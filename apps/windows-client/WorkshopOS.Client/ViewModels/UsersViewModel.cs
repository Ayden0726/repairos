using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private IReadOnlyList<StaffUserDto> _users = Array.Empty<StaffUserDto>();
    [ObservableProperty] private IReadOnlyList<RoleDto> _roles = Array.Empty<RoleDto>();
    [ObservableProperty] private StaffUserDto? _selectedUser;

    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private RoleDto? _newRole;

    [ObservableProperty] private RoleDto? _editRole;
    [ObservableProperty] private string _editStatus = "Active";

    public IReadOnlyList<string> StatusOptions { get; } = ["Active", "Suspended"];

    public UsersViewModel(ApiClient api) => _api = api;

    partial void OnSelectedUserChanged(StaffUserDto? value)
    {
        if (value is null)
        {
            EditRole = null;
            EditStatus = "Active";
            return;
        }
        EditRole = Roles.FirstOrDefault(r => r.Key == value.RoleKey);
        EditStatus = value.Status;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        Status = null;
        try
        {
            var usersTask = _api.GetAsync<IReadOnlyList<StaffUserDto>>("api/users");
            IReadOnlyList<RoleDto> roles;
            try
            {
                roles = await _api.GetAsync<IReadOnlyList<RoleDto>>("api/roles");
            }
            catch
            {
                // staff.view without roles.manage — still list users; role picker empty for create.
                roles = Array.Empty<RoleDto>();
            }

            Users = await usersTask;
            Roles = roles.Where(r => !string.Equals(r.Key, "owner", StringComparison.OrdinalIgnoreCase)).ToList();
            if (NewRole is null) NewRole = Roles.FirstOrDefault();
            if (Users.Count == 0)
                Status = "No staff accounts yet. Create one below.";
            OnSelectedUserChanged(SelectedUser);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Users = Array.Empty<StaffUserDto>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = null;
        Status = null;
        if (NewRole is null)
        {
            Error = "Select a role.";
            return;
        }

        IsSaving = true;
        try
        {
            await _api.PostAsync<CreateStaffUserRequest, StaffUserDto>(
                "api/users",
                new CreateStaffUserRequest(NewEmail.Trim(), NewDisplayName.Trim(), NewPassword, NewRole.Key,
                    string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim()));
            NewEmail = string.Empty;
            NewDisplayName = string.Empty;
            NewPassword = string.Empty;
            NewPhone = string.Empty;
            Status = "Account created.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task SaveSelectedAsync()
    {
        if (SelectedUser is null) return;
        Error = null;
        Status = null;
        IsSaving = true;
        try
        {
            await _api.PutAsync<UpdateStaffUserRequest, StaffUserDto>(
                $"api/users/{SelectedUser.Id}",
                new UpdateStaffUserRequest(
                    SelectedUser.DisplayName,
                    EditRole?.Key,
                    EditStatus,
                    SelectedUser.Phone));
            Status = "User updated.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
