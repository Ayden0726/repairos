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

    public static RoleOption From(RoleDto r) => new()
    {
        Id = r.Id,
        Key = r.Key,
        DisplayLabel = r.Name,
        Description = r.Description
    };
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
    [ObservableProperty] private StaffUserDto? _selectedUser;

    [ObservableProperty] private string _newEmail = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _newPhone = string.Empty;
    [ObservableProperty] private RoleOption? _newRole;

    [ObservableProperty] private RoleOption? _editRole;
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

            List<RoleDto> roleDtos = [];
            string? rolesError = null;
            try
            {
                var fetched = await _api.GetAsync<List<RoleDto>>("api/roles");
                roleDtos = fetched ?? [];
            }
            catch (Exception ex)
            {
                rolesError = $"Roles could not be loaded ({ex.Message}). You need staff.view to list roles.";
                // Fallback: try raw parse in case Permissions shape drifted.
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
                            if (!string.IsNullOrWhiteSpace(key))
                                roleDtos.Add(new RoleDto(id, key, name, desc, []));
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
            foreach (var r in roleDtos.Where(r => !string.Equals(r.Key, "owner", StringComparison.OrdinalIgnoreCase)))
                Roles.Add(RoleOption.From(r));

            if (NewRole is null || Roles.All(r => r.Key != NewRole.Key))
                NewRole = Roles.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(rolesError))
                Error = rolesError;
            else if (Roles.Count == 0)
                Error = "No assignable roles returned from GET /api/roles.";
            else if (Users.Count == 0)
                Status = "No staff accounts yet. Create one below.";
            else
                Status = $"{Users.Count} account(s) · {Roles.Count} role(s)";

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
