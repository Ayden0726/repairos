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
    public ObservableCollection<LookupDto> Priorities { get; } = new();
    public ObservableCollection<StaffLookupDto> Technicians { get; } = new();
    public ObservableCollection<string> StatusFilterLabels { get; } = new();
    public ObservableCollection<string> PriorityFilterLabels { get; } = new();
    public ObservableCollection<string> TechnicianFilterLabels { get; } = new();

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _overdueOnly;
    [ObservableProperty] private string _selectedStatusLabel = "All statuses";
    [ObservableProperty] private string _selectedPriorityLabel = "All priorities";
    [ObservableProperty] private string _selectedTechnicianLabel = "All technicians";
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
                Priorities.Clear();
                Technicians.Clear();
                StatusFilterLabels.Clear();
                PriorityFilterLabels.Clear();
                TechnicianFilterLabels.Clear();
                StatusFilterLabels.Add("All statuses");
                PriorityFilterLabels.Add("All priorities");
                TechnicianFilterLabels.Add("All technicians");
                foreach (var s in lookups.Statuses)
                {
                    Statuses.Add(s);
                    StatusFilterLabels.Add(s.Name);
                }
                foreach (var p in lookups.Priorities)
                {
                    Priorities.Add(p);
                    PriorityFilterLabels.Add(p.Name);
                }
                foreach (var t in lookups.Technicians)
                {
                    Technicians.Add(t);
                    TechnicianFilterLabels.Add(t.Name);
                }
                ApplyPendingStatusLabel();
            }

            var statusKey = ResolveStatusKey();
            var priorityKey = ResolvePriorityKey();
            var techId = ResolveTechnicianId();
            var qs = $"api/repairs?q={Uri.EscapeDataString(Query)}&pageSize=100";
            if (!string.IsNullOrWhiteSpace(statusKey))
                qs += $"&status={Uri.EscapeDataString(statusKey)}";
            if (!string.IsNullOrWhiteSpace(priorityKey))
                qs += $"&priority={Uri.EscapeDataString(priorityKey)}";
            if (techId is Guid tid)
                qs += $"&assignedToId={tid}";
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

    private string? ResolvePriorityKey()
    {
        if (string.IsNullOrWhiteSpace(SelectedPriorityLabel) || SelectedPriorityLabel == "All priorities")
            return null;
        return Priorities.FirstOrDefault(p => p.Name == SelectedPriorityLabel)?.Key;
    }

    private Guid? ResolveTechnicianId()
    {
        if (string.IsNullOrWhiteSpace(SelectedTechnicianLabel) || SelectedTechnicianLabel == "All technicians")
            return null;
        return Technicians.FirstOrDefault(t => t.Name == SelectedTechnicianLabel)?.Id;
    }
}

public partial class NewRepairViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private Guid? _preferCustomerId;
    public ObservableCollection<CustomerListItemDto> Customers { get; } = new();
    public ObservableCollection<LookupDto> Types { get; } = new();
    public ObservableCollection<LookupDto> Priorities { get; } = new();
    public ObservableCollection<StaffLookupDto> Technicians { get; } = new();

    [ObservableProperty] private bool _useExistingCustomer = true;
    [ObservableProperty] private string _customerQuery = string.Empty;
    [ObservableProperty] private bool _isLoadingCustomers;
    [ObservableProperty] private string? _customersStatus;
    [ObservableProperty] private CustomerListItemDto? _selectedCustomer;
    [ObservableProperty] private string _newFirstName = string.Empty;
    [ObservableProperty] private string _newLastName = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private string _newEmail = string.Empty;
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

    public bool ShowNewCustomerFields => !UseExistingCustomer;
    public bool HasCustomers => Customers.Count > 0;
    public bool HasCustomersStatus => !string.IsNullOrWhiteSpace(CustomersStatus);
    public bool IsCustomersIdle => !IsLoadingCustomers;
    public bool CanSubmit => !IsSaving;

    public NewRepairViewModel(ApiClient api) => _api = api;

    partial void OnUseExistingCustomerChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNewCustomerFields));
        Error = null;
    }

    partial void OnIsLoadingCustomersChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCustomersIdle));
    }

    partial void OnCustomersStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasCustomersStatus));
    }

    partial void OnIsSavingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
    }

    public async Task InitAsync(Guid? customerId)
    {
        _preferCustomerId = customerId;
        Error = null;
        CustomersStatus = null;
        try
        {
            var lookups = await _api.GetAsync<RepairLookupsDto>("api/repairs/lookups");
            Types.Clear(); foreach (var t in lookups.Types) Types.Add(t);
            Priorities.Clear(); foreach (var p in lookups.Priorities) Priorities.Add(p);
            Technicians.Clear(); foreach (var t in lookups.Technicians) Technicians.Add(t);
            SelectedType ??= Types.FirstOrDefault();
            SelectedPriority ??= Priorities.FirstOrDefault(p => p.Key is "normal" or "medium") ?? Priorities.FirstOrDefault();

            if (customerId is Guid)
                UseExistingCustomer = true;

            await LoadCustomersAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SearchCustomersAsync() => await LoadCustomersAsync();

    private async Task LoadCustomersAsync()
    {
        IsLoadingCustomers = true;
        CustomersStatus = null;
        Error = null;
        try
        {
            var q = Uri.EscapeDataString(CustomerQuery?.Trim() ?? string.Empty);
            var url = string.IsNullOrEmpty(q)
                ? "api/customers?pageSize=200"
                : $"api/customers?q={q}&pageSize=100";
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>(url);
            Customers.Clear();
            foreach (var c in page.Items) Customers.Add(c);
            OnPropertyChanged(nameof(HasCustomers));

            if (_preferCustomerId is Guid prefer)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == prefer) ?? SelectedCustomer;
                _preferCustomerId = null;
            }
            else if (SelectedCustomer is not null)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == SelectedCustomer.Id);
            }

            if (Customers.Count == 0)
            {
                CustomersStatus = string.IsNullOrWhiteSpace(CustomerQuery)
                    ? "No customers yet. Uncheck Existing customer to create one here."
                    : "No customers match that search.";
                SelectedCustomer = null;
            }
            else
            {
                CustomersStatus = $"{Customers.Count} customer{(Customers.Count == 1 ? "" : "s")}";
                SelectedCustomer ??= Customers[0];
            }
        }
        catch (Exception ex)
        {
            Customers.Clear();
            OnPropertyChanged(nameof(HasCustomers));
            SelectedCustomer = null;
            CustomersStatus = null;
            Error = $"Could not load customers: {ex.Message}";
        }
        finally
        {
            IsLoadingCustomers = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Issue)) { Error = "Describe the fault."; return; }

        if (UseExistingCustomer)
        {
            if (SelectedCustomer is null)
            {
                Error = HasCustomers
                    ? "Select a customer."
                    : "No customer selected. Search again or create a new customer.";
                return;
            }
        }
        else if (string.IsNullOrWhiteSpace(NewFirstName) && string.IsNullOrWhiteSpace(NewLastName)
                 && string.IsNullOrWhiteSpace(NewPhone) && string.IsNullOrWhiteSpace(NewEmail))
        {
            Error = "Enter a name, phone, or email for the new customer.";
            return;
        }

        IsSaving = true;
        try
        {
            Guid customerId;
            if (UseExistingCustomer)
            {
                customerId = SelectedCustomer!.Id;
            }
            else
            {
                var created = await _api.PostAsync<UpsertCustomerRequest, CustomerDetailDto>(
                    "api/customers",
                    new UpsertCustomerRequest(
                        null,
                        CustomerType.Individual,
                        string.IsNullOrWhiteSpace(NewFirstName) ? null : NewFirstName.Trim(),
                        string.IsNullOrWhiteSpace(NewLastName) ? null : NewLastName.Trim(),
                        null,
                        string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
                        string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail.Trim(),
                        null, null, null, null, null,
                        PreferredContact.Sms,
                        false));
                customerId = created.Id;
            }

            var repair = await _api.PostAsync<CreateRepairRequest, RepairDetailDto>("api/repairs", new CreateRepairRequest(
                customerId, null, SelectedType?.Id, SelectedPriority?.Id, SelectedTechnician?.Id,
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
    [ObservableProperty] private bool _noteIsInternal;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _recommended = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _accessoriesSummary = string.Empty;
    public ObservableCollection<LookupDto> Statuses { get; } = new();
    public ObservableCollection<LookupDto> Priorities { get; } = new();
    public ObservableCollection<StaffLookupDto> Technicians { get; } = new();
    [ObservableProperty] private LookupDto? _selectedStatus;
    [ObservableProperty] private LookupDto? _selectedPriority;
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
            Priorities.Clear();
            foreach (var p in lookups.Priorities) Priorities.Add(p);
            Technicians.Clear();
            foreach (var t in lookups.Technicians) Technicians.Add(t);
            Repair = await _api.GetAsync<RepairDetailDto>($"api/repairs/{id}");
            SelectedStatus = Statuses.FirstOrDefault(s => s.Id == Repair.StatusId);
            SelectedPriority = Priorities.FirstOrDefault(p => p.Id == Repair.PriorityId);
            SelectedTechnician = Technicians.FirstOrDefault(t => t.Id == Repair.AssignedToId);
            Diagnosis = Repair.Diagnosis ?? string.Empty;
            Recommended = Repair.RecommendedRepair ?? string.Empty;
            AccessoriesSummary = BuildAccessories(Repair);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    private static string BuildAccessories(RepairDetailDto r)
    {
        var parts = new List<string>();
        if (r.ChargerIncluded) parts.Add("Charger");
        if (r.SimIncluded) parts.Add("SIM");
        if (r.CaseIncluded) parts.Add("Case");
        if (r.AccessoriesIncluded) parts.Add("Other accessories");
        if (r.HasPasscode) parts.Add("Passcode on file");
        return parts.Count == 0 ? "No accessories noted." : "Intake: " + string.Join(" · ", parts);
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
            AccessoriesSummary = BuildAccessories(Repair);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SavePriorityAsync()
    {
        if (Repair is null || SelectedPriority is null) return;
        IsBusy = true;
        Error = null;
        try
        {
            Repair = await _api.PostAsync<ChangePriorityRequest, RepairDetailDto>(
                $"api/repairs/{Repair.Id}/priority", new ChangePriorityRequest(SelectedPriority.Id));
            StatusMessage = $"Priority → {Repair.PriorityName}";
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
            await _api.PostAsync<AddNoteRequest, RepairNoteDto>(
                $"api/repairs/{Repair.Id}/notes",
                new AddNoteRequest(NoteBody.Trim(), NoteIsInternal));
            NoteBody = string.Empty;
            NoteIsInternal = false;
            StatusMessage = "Note added.";
            await LoadAsync(Repair.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task PrintAsync()
    {
        if (Repair is null) return;
        Error = null;
        try
        {
            var html = await _api.GetRawAsync($"api/repairs/{Repair.Id}/print");
            var dir = Path.Combine(Path.GetTempPath(), "WorkshopOS");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{Repair.TicketNumber.Replace('/', '-')}-jobsheet.html");
            await File.WriteAllTextAsync(path, html);
            var uri = new Uri(path);
            var success = await Windows.System.Launcher.LaunchUriAsync(uri);
            StatusMessage = success ? "Opened job sheet for printing." : "Saved job sheet but could not open browser.";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
