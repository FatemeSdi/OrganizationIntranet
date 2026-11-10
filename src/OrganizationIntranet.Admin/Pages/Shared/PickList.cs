using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Pages.Shared;

/// <summary>
/// مدل partial «_EntityPicker»: انتخاب کاربر یا نقش با فهرست کشویی قابل جست‌وجو؛ موارد انتخاب‌شده زیر آن به‌صورت رادیوباتن‌های تیک‌خورده نمایش داده می‌شوند
/// و مقدارشان با همان نام فیلد فرم (hidden) ارسال می‌شود. اگر InstantUrl داده شود، انتخاب و برداشتن هر مورد همان لحظه با fetch ثبت می‌شود
/// (بدون دکمه‌ی ذخیره) و toast نشان می‌دهد؛ وگرنه فقط فرم را تغییر می‌دهد.
/// Kind: user یا role (برای بدنه‌ی درخواست لحظه‌ای)؛ Noun: «کاربر» یا «نقش» برای متن‌ها.
/// </summary>
public sealed record PickList(string Name, string Label, IReadOnlyList<PickItem> Items, string SearchPlaceholder = "جست‌وجو...", string Kind = "user", string Noun = "کاربر", string? InstantUrl = null, bool ReadOnly = false)
{
    public static PickList ForUsers(string name, string label, IEnumerable<UserDto> users, ICollection<long> selected, string? instantUrl = null, bool readOnly = false) =>
        new(name, label, users.Select(u => new PickItem(u.UserId.ToString(), $"{u.Name} {u.LastName}".Trim(), u.Username, selected.Contains(u.UserId), !u.IsActive)).ToList(),
            "کاربر را انتخاب کنید", "user", "کاربر", instantUrl, readOnly);

    public static PickList ForRoles(string name, string label, IEnumerable<RoleDto> roles, ICollection<int> selected, string? instantUrl = null, bool readOnly = false) =>
        new(name, label, roles.Select(r => new PickItem(r.RoleId.ToString(), r.RoleName, r.RoleCode, selected.Contains(r.RoleId), !r.IsActive)).ToList(),
            "نقش را انتخاب کنید", "role", "نقش", instantUrl, readOnly);
}

/// <summary>یک گزینه‌ی انتخاب‌گر (شناسه، عنوان و متن جست‌وجو).</summary>
public sealed record PickItem(string Value, string Label, string? Sub, bool Selected, bool Inactive);
