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
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private string? _error;

    public RepairsViewModel(ApiClient api) => _api = api;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        Error = null;
        try
        {
            var page = await _api.GetAsync<PagedResult<RepairListItemDto>>($"api/repairs?q={Uri.EscapeDataString(Query)}&pageSize=100");
            Items.Clear();
            foreach (var item in page.Items) Items.Add(item);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class NewRepairViewModel : ObservableObject
{
    private readonly ApiClient _api;
    public ObservableCollection<CustomerListItemDto> Customers { get; } = new();
    [ObservableProperty] private CustomerListItemDto? _selectedCustomer;
    [ObservableProperty] private string _brand = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _issue = string.Empty;
    [ObservableProperty] private string? _error;
    public Action<Guid>? Created { get; set; }

    public NewRepairViewModel(ApiClient api) => _api = api;

    public async Task InitAsync(Guid? customerId)
    {
        try
        {
            var page = await _api.GetAsync<PagedResult<CustomerListItemDto>>("api/customers?pageSize=200");
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
        try
        {
            var repair = await _api.PostAsync<CreateRepairRequest, RepairDetailDto>("api/repairs", new CreateRepairRequest(
                SelectedCustomer.Id, null, null, null, null, Issue.Trim(), null, null, null, null, true,
                true, false, false, false, null, null, null, null,
                DeviceCategory.Phone, string.IsNullOrWhiteSpace(Brand) ? "Unknown" : Brand, string.IsNullOrWhiteSpace(Model) ? "Device" : Model, null, null));
            Created?.Invoke(repair.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}

public partial class RepairDetailViewModel : ObservableObject
{
    private readonly ApiClient _api;
    [ObservableProperty] private RepairDetailDto? _repair;
    [ObservableProperty] private string _noteBody = string.Empty;
    [ObservableProperty] private string? _error;
    public ObservableCollection<LookupDto> Statuses { get; } = new();
    [ObservableProperty] private LookupDto? _selectedStatus;

    public RepairDetailViewModel(ApiClient api) => _api = api;

    public async Task LoadAsync(Guid id)
    {
        try
        {
            var lookups = await _api.GetAsync<RepairLookupsDto>("api/repairs/lookups");
            Statuses.Clear();
            foreach (var s in lookups.Statuses) Statuses.Add(s);
            Repair = await _api.GetAsync<RepairDetailDto>($"api/repairs/{id}");
            SelectedStatus = Statuses.FirstOrDefault(s => s.Id == Repair.StatusId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveStatusAsync()
    {
        if (Repair is null || SelectedStatus is null) return;
        try
        {
            Repair = await _api.PostAsync<ChangeStatusRequest, RepairDetailDto>($"api/repairs/{Repair.Id}/status", new ChangeStatusRequest(SelectedStatus.Id));
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        if (Repair is null || string.IsNullOrWhiteSpace(NoteBody)) return;
        try
        {
            await _api.PostAsync<AddNoteRequest, RepairNoteDto>($"api/repairs/{Repair.Id}/notes", new AddNoteRequest(NoteBody, false));
            NoteBody = string.Empty;
            await LoadAsync(Repair.Id);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
