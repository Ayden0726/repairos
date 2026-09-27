using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkshopOS.Client.Services;
using WorkshopOS.Contracts.Auth;
using WorkshopOS.Contracts.Common;

namespace WorkshopOS.Client.ViewModels;

/// <summary>
/// WinUI ComboBox DisplayMemberPath="Name" conflicts with FrameworkElement.Name and often shows blank.
/// Use DisplayLabel instead.
/// </summary>
public sealed class RoleOption
{
    public required Guid Id { get; init; }
    public required string Key { get; init; }
    public required string DisplayLabel { get; init; }
    public string? Description { get; init; }
    public bool IsSystem { get; init; }
    public string[] Permissions { get; init; } = [];

    public static RoleOption From(RoleDto r) => new()
    {
        Id = r.Id,
        Key = r.Key,
        DisplayLabel = r.Name,
        Description = r.Description,
        IsSystem = r.IsSystem,
        Permissions = r.Permissions ?? []
    };
}

public partial class PermissionToggleItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Group { get; init; }
    public required string Label { get; init; }
    [ObservableProperty] private bool _isChecked;

    public string Display => $"{Group}: {Label}";
}

public partial class UsersViewModel : ObservableObject
{
    private readonly ApiClient _api;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _status;
    [ObservableProperty] private IReadOnlyList<StaffUserDto> _users = Array.Empty<StaffUserDto>();
    [ObservableProperty] private ObservableCollection<RoleOption> _roles = new();
    [ObservableProperty] private ObservableCollection<RoleOption> _allRoles = new();
    [ObservableProperty] private StaffUserDto? _selectedUser;
    [ObservableProperty] private RoleOption? _selectedRole;

    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private RoleOption? _newRole;

    [ObservableProperty] private RoleOption? _editRole;
    [ObservableProperty] private string _editStatus = "Active";

    [ObservableProperty] private string _roleName = string.Empty;
    [ObservableProperty] private string _roleKey = string.Empty;
    [ObservableProperty] private string _roleDescription = string.Empty;
    [ObservableProperty] private string? _rolesStatus;
    [ObservableProperty] private string? _rolesError;
    [ObservableProperty] private bool _canManageRoles;

    public ObservableCollection<PermissionToggleItem> PermissionToggles { get; } = new();

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

    partial void OnSelectedRoleChanged(RoleOption? value)
    {
        if (value is null)
        {
            RoleName = string.Empty;
            RoleKey = string.Empty;
            RoleDescription = string.Empty;
            foreach (var p in PermissionToggles) p.IsChecked = false;
            return;
        }

        RoleName = value.DisplayLabel;
        RoleKey = value.Key;
        RoleDescription = value.Description ?? string.Empty;
        var set = value.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var p in PermissionToggles)
            p.IsChecked = set.Contains(p.Key);
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        Status = null;
        RolesError = null;
        RolesStatus = null;
        try
        {
            var usersTask = _api.GetAsync<IReadOnlyList<StaffUserDto>>("api/users");

            List<RoleDto> roleDtos = [];
            string? rolesError = null;
            try
            {
                var fetched = await _api.GetAsync<List<RoleDto>>("api/roles");
                roleDtos = fetched ?? [];
            }
            catch (Exception ex)
            {
                rolesError = FriendlyRolesError(ex.Message);
                try
                {
                    var raw = await _api.GetRawAsync("api/roles");
                    using var doc = System.Text.Json.JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var id = el.TryGetProperty("id", out var idEl) && idEl.TryGetGuid(out var g) ? g : Guid.Empty;
                            var key = el.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
                            var name = el.TryGetProperty("name", out var n) ? n.GetString() ?? key : key;
                            var desc = el.TryGetProperty("description", out var d) ? d.GetString() : null;
                            var isSystem = el.TryGetProperty("isSystem", out var sys) && sys.ValueKind == System.Text.Json.JsonValueKind.True;
                            if (!string.IsNullOrWhiteSpace(key))
                                roleDtos.Add(new RoleDto(id, key, name, desc, [], isSystem));
                        }
                        if (roleDtos.Count > 0) rolesError = null;
                    }
                }
                catch
                {
                    /* keep rolesError */
                }
            }

            Users = await usersTask;
            Roles.Clear();
            AllRoles.Clear();
            foreach (var r in roleDtos)
            {
                var opt = RoleOption.From(r);
                AllRoles.Add(opt);
                if (!string.Equals(r.Key, "owner", StringComparison.OrdinalIgnoreCase))
                    Roles.Add(opt);
            }

            if (NewRole is null || Roles.All(r => r.Key != NewRole.Key))
                NewRole = Roles.FirstOrDefault();

            await LoadPermissionsCatalogueAsync();

            if (!string.IsNullOrWhiteSpace(rolesError))
                Error = rolesError;
            else if (Roles.Count == 0)
                Error = "No assignable roles returned from GET /api/roles. Update/restart the WorkshopOS server if this persists.";
            else if (Users.Count == 0)
                Status = "No staff accounts yet. Create one below.";
            else
                Status = $"{Users.Count} account(s) · {Roles.Count} assignable role(s)";

            RolesStatus = AllRoles.Count == 0
                ? "Default roles missing — update the server so DbSeed can create Owner/Admin/Manager/Technician/Front Desk/Sales/Read Only."
                : $"{AllRoles.Count} role(s). Owner/Admin can add custom roles with permission checkboxes.";

            OnSelectedUserChanged(SelectedUser);
            if (SelectedRole is not null)
                SelectedRole = AllRoles.FirstOrDefault(r => r.Id == SelectedRole.Id);
        }
        catch (Exception ex)
        {
            Error = FriendlyRolesError(ex.Message);
            Users = Array.Empty<StaffUserDto>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadPermissionsCatalogueAsync()
    {
        CanManageRoles = false;
        try
        {
            var perms = await _api.GetAsync<IReadOnlyList<PermissionDto>>("api/permissions");
            CanManageRoles = true;
            PermissionToggles.Clear();
            foreach (var p in perms)
                PermissionToggles.Add(new PermissionToggleItem { Key = p.Key, Group = p.Group, Label = p.Label });
            OnSelectedRoleChanged(SelectedRole);
        }
        catch
        {
            // staff without roles.manage still sees roles list for assignment
            CanManageRoles = false;
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
            Error = FriendlyRolesError(ex.Message);
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
            Error = FriendlyRolesError(ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void NewRoleDraft()
    {
        SelectedRole = null;
        RoleName = string.Empty;
        RoleKey = string.Empty;
        RoleDescription = string.Empty;
        foreach (var p in PermissionToggles) p.IsChecked = false;
        RolesStatus = "New custom role — set name/key and permissions, then Save role.";
    }

    [RelayCommand]
    private async Task SaveRoleAsync()
    {
        RolesError = null;
        RolesStatus = null;
        if (!CanManageRoles)
        {
            RolesError = "You need roles.manage (Owner/Admin) to create or edit roles.";
            return;
        }
        if (string.IsNullOrWhiteSpace(RoleName))
        {
            RolesError = "Role name is required.";
            return;
        }

        IsSaving = true;
        try
        {
            var perms = PermissionToggles.Where(p => p.IsChecked).Select(p => p.Key).ToArray();
            if (SelectedRole is null || SelectedRole.Id == Guid.Empty)
            {
                var created = await _api.PostAsync<CreateRoleRequest, RoleDto>(
                    "api/roles",
                    new CreateRoleRequest(
                        string.IsNullOrWhiteSpace(RoleKey) ? RoleName : RoleKey,
                        RoleName.Trim(),
                        string.IsNullOrWhiteSpace(RoleDescription) ? null : RoleDescription.Trim(),
                        perms));
                RolesStatus = $"Created role {created.Name}.";
            }
            else
            {
                if (string.Equals(SelectedRole.Key, "owner", StringComparison.OrdinalIgnoreCase))
                {
                    RolesError = "Owner role cannot be edited.";
                    return;
                }
                var updated = await _api.PutAsync<UpdateRoleRequest, RoleDto>(
                    $"api/roles/{SelectedRole.Id}",
                    new UpdateRoleRequest(
                        RoleName.Trim(),
                        string.IsNullOrWhiteSpace(RoleDescription) ? null : RoleDescription.Trim(),
                        perms));
                RolesStatus = $"Updated role {updated.Name}.";
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            RolesError = FriendlyRolesError(ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static string FriendlyRolesError(string message)
    {
        if (message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
            return "Server outdated — update/restart WorkshopOS server so GET /api/roles and /api/users exist. " + message;
        return message;
    }
}
