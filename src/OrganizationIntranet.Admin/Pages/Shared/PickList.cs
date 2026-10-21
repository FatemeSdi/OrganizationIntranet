using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Pages.Shared;

/// <summary>مدل partial «_PickList»: فهرست چک‌باکسی قابل جست‌وجو به‌جای select چندگانه؛ مقادیر با همان نام فیلد فرم ارسال می‌شوند.</summary>
public sealed record PickList(string Name, string Label, IReadOnlyList<PickItem> Items, string SearchPlaceholder = "جست‌وجو...")
{
    public static PickList ForUsers(string name, string label, IEnumerable<UserDto> users, ICollection<long> selected) =>
        new(name, label, users.Select(u => new PickItem(u.UserId.ToString(), $"{u.Name} {u.LastName}".Trim(), u.Username, selected.Contains(u.UserId), !u.IsActive)).ToList(),
            "جست‌وجوی نام یا نام کاربری");

    public static PickList ForRoles(string name, string label, IEnumerable<RoleDto> roles, ICollection<int> selected) =>
        new(name, label, roles.Select(r => new PickItem(r.RoleId.ToString(), r.RoleName, r.RoleCode, selected.Contains(r.RoleId), !r.IsActive)).ToList(),
            "جست‌وجوی نقش");
}

public sealed record PickItem(string Value, string Label, string? Sub, bool Selected, bool Inactive);
