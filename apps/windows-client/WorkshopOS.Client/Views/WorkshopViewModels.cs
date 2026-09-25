using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Workshop;

namespace WorkshopOS.Client.Views;

public partial class CustomersViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<CustomerListItemDto> Items { get; } = new();
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;

    public CustomersViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        try
        {
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>($"api/customers?q={Uri.EscapeDataString(Query)}&pageSize=100");
            Items.Clear();
            foreach (var item in page.Items) Items.Add(item);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        try
        {
            await _api.PostAsync<UpsertCustomerRequest, CustomerDetailDto>("api/customers", new UpsertCustomerRequest(
                null, CustomerType.Individual, FirstName, LastName, null, Phone, Email, null, null, null, null, null, PreferredContact.Sms, false));
            FirstName = LastName = Phone = Email = string.Empty;
            await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class CustomerDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private CustomerDetailDto? _customer;
    [ObservableProperty] private string? _error;

    public CustomerDetailViewModel(ApiClient api) => _api = api;

    public async Task LoadAsync(Guid id)
    {
        try { Customer = await _api.GetAsync<CustomerDetailDto>($"api/customers/{id}"); }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class RepairsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<RepairListItemDto> Items { get; } = new();
    public ObservableCollection<LookupDto> Statuses { get; } = new();
    public ObservableCollection<string> StatusFilterLabels { get; } = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _overdueOnly;
    [ObservableProperty] private string _selectedStatusLabel = "All statuses";
    [ObservableProperty] private int _totalCount;

    private string? _pendingFilter;

    public RepairsViewModel(ApiClient api) => _api = api;

    public void ApplyIncomingFilter(string? filter)
    {
        _pendingFilter = filter;
        if (string.IsNullOrWhiteSpace(filter)) return;
        if (string.Equals(filter, "overdue", StringComparison.OrdinalIgnoreCase))
        {
            OverdueOnly = true;
            SelectedStatusLabel = "All statuses";
            return;
        }
        OverdueOnly = false;
        // Map dashboard card keys to status keys / labels after lookups load.
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        IsLoading = true;
        try
        {
            if (Statuses.Count == 0)
            {
                var lookups = await _api.GetAsync<RepairLookupsDto>("api/repairs/lookups");
                Statuses.Clear();
                StatusFilterLabels.Clear();
                StatusFilterLabels.Add("All statuses");
                foreach (var s in lookups.Statuses)
                {
                    Statuses.Add(s);
                    StatusFilterLabels.Add(s.Name);
                }
                ApplyPendingStatusLabel();
            }

            var statusKey = ResolveStatusKey();
            var qs = $"api/repairs?q={Uri.EscapeDataString(Query)}&pageSize=100";
            if (!string.IsNullOrWhiteSpace(statusKey))
                qs += $"&status={Uri.EscapeDataString(statusKey)}";
            if (OverdueOnly)
                qs += "&overdue=true";

            var page = await _api.GetAsync<PagedResult<RepairListItemDto>>(qs);
            Items.Clear();
            foreach (var item in page.Items) Items.Add(item);
            TotalCount = page.Total;
            StatusMessage = page.Total == 0
                ? "No tickets match these filters."
                : $"{page.Total} ticket(s)";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            StatusMessage = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyPendingStatusLabel()
    {
        if (string.IsNullOrWhiteSpace(_pendingFilter)) return;
        var key = _pendingFilter;
        _pendingFilter = null;
        if (string.Equals(key, "overdue", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "open", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "dueToday", StringComparison.OrdinalIgnoreCase))
            return;

        var match = Statuses.FirstOrDefault(s =>
            string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            SelectedStatusLabel = match.Name;
    }

    private string? ResolveStatusKey()
    {
        if (string.IsNullOrWhiteSpace(SelectedStatusLabel) || SelectedStatusLabel == "All statuses")
            return null;
        return Statuses.FirstOrDefault(s => s.Name == SelectedStatusLabel)?.Key;
    }
}

public partial class NewRepairViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<CustomerListItemDto> Customers { get; } = new();
    public ObservableCollection<LookupDto> Types { get; } = new();
    public ObservableCollection<LookupDto> Priorities { get; } = new();
    public ObservableCollection<StaffLookupDto> Technicians { get; } = new();

    [ObservableProperty] private CustomerListItemDto? _selectedCustomer;
    [ObservableProperty] private LookupDto? _selectedType;
    [ObservableProperty] private LookupDto? _selectedPriority;
    [ObservableProperty] private StaffLookupDto? _selectedTechnician;
    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _issue = string.Empty;
    [ObservableProperty] private string _serial = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isSaving;
    public Action<Guid>? Created { get; set; }

    public NewRepairViewModel(ApiClient api) => _api = api;

    public async Task InitAsync(Guid? customerId)
    {
        try
        {
            var pageTask = _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?pageSize=200");
            var lookups = await _api.GetAsync<RepairLookupsDto>("api/repairs/lookups");
            Types.Clear(); foreach (var t in lookups.Types) Types.Add(t);
            Priorities.Clear(); foreach (var p in lookups.Priorities) Priorities.Add(p);
            Technicians.Clear(); foreach (var t in lookups.Technicians) Technicians.Add(t);
            SelectedType ??= Types.FirstOrDefault();
            SelectedPriority ??= Priorities.FirstOrDefault(p => p.Key is "normal" or "medium") ?? Priorities.FirstOrDefault();

            var page = await pageTask;
            Customers.Clear();
            foreach (var c in page.Items) Customers.Add(c);
            if (customerId is Guid id)
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == id);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        if (SelectedCustomer is null) { Error = "Select a customer."; return; }
        if (string.IsNullOrWhiteSpace(Issue)) { Error = "Describe the fault."; return; }
        IsSaving = true;
        try
        {
            var repair = await _api.PostAsync<CreateRepairRequest, RepairDetailDto>("api/repairs", new CreateRepairRequest(
                SelectedCustomer.Id, null, SelectedType?.Id, SelectedPriority?.Id, SelectedTechnician?.Id,
                Issue.Trim(), null, null, null, null, true,
                true, false, false, false, null, null, null, null,
                DeviceCategory.Phone,
                string.IsNullOrWhiteSpace(Brand) ? "Unknown" : Brand.Trim(),
                string.IsNullOrWhiteSpace(Model) ? "Device" : Model.Trim(),
                string.IsNullOrWhiteSpace(Serial) ? null : Serial.Trim(),
                null));
            Created?.Invoke(repair.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsSaving = false; }
    }
}

public partial class RepairDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private RepairDetailDto? _repair;
    [ObservableProperty] private string _noteBody = string.Empty;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _recommended = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    public ObservableCollection<LookupDto> Statuses { get; } = new();
    public ObservableCollection<StaffLookupDto> Technicians { get; } = new();
    [ObservableProperty] private LookupDto? _selectedStatus;
    [ObservableProperty] private StaffLookupDto? _selectedTechnician;

    public RepairDetailViewModel(ApiClient api) => _api = api;

    public async Task LoadAsync(Guid id)
    {
        Error = null;
        try
        {
            var lookups = await _api.GetAsync<RepairLookupsDto>("api/repairs/lookups");
            Statuses.Clear();
            foreach (var s in lookups.Statuses) Statuses.Add(s);
            Technicians.Clear();
            foreach (var t in lookups.Technicians) Technicians.Add(t);
            Repair = await _api.GetAsync<RepairDetailDto>($"api/repairs/{id}");
            SelectedStatus = Statuses.FirstOrDefault(s => s.Id == Repair.StatusId);
            SelectedTechnician = Technicians.FirstOrDefault(t => t.Id == Repair.AssignedToId);
            Diagnosis = Repair.Diagnosis ?? string.Empty;
            Recommended = Repair.RecommendedRepair ?? string.Empty;
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (Repair is null || SelectedStatus is null) return;
        IsBusy = true;
        Error = null;
        try
        {
            Repair = await _api.PostAsync<ChangeStatusRequest, RepairDetailDto>($"api/repairs/{Repair.Id}/status", new ChangeStatusRequest(SelectedStatus.Id));
            StatusMessage = $"Status → {Repair.StatusName}";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAssignAsync()
    {
        if (Repair is null) return;
        IsBusy = true;
        Error = null;
        try
        {
            Repair = await _api.PostAsync<AssignRepairRequest, RepairDetailDto>(
                $"api/repairs/{Repair.Id}/assign", new AssignRepairRequest(SelectedTechnician?.Id));
            StatusMessage = SelectedTechnician is null ? "Unassigned." : $"Assigned to {SelectedTechnician.Name}";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveDiagnosisAsync()
    {
        if (Repair is null) return;
        IsBusy = true;
        Error = null;
        try
        {
            Repair = await _api.PostAsync<UpdateDiagnosisRequest, RepairDetailDto>(
                $"api/repairs/{Repair.Id}/diagnosis",
                new UpdateDiagnosisRequest(
                    string.IsNullOrWhiteSpace(Diagnosis) ? null : Diagnosis.Trim(),
                    string.IsNullOrWhiteSpace(Recommended) ? null : Recommended.Trim()));
            StatusMessage = "Diagnosis saved.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        if (Repair is null || string.IsNullOrWhiteSpace(NoteBody)) return;
        IsBusy = true;
        Error = null;
        try
        {
            await _api.PostAsync<AddNoteRequest, RepairNoteDto>($"api/repairs/{Repair.Id}/notes", new AddNoteRequest(NoteBody.Trim(), false));
            NoteBody = string.Empty;
            StatusMessage = "Note added.";
            await LoadAsync(Repair.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }
}
