extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// کتابخانه‌ی اسناد (migration 017): مدیریت دسته‌بندی و سند در پنل، ذخیره‌ی فایل روی دیسک، نمایش و دانلود عمومی در پورتال.
public sealed partial class PortalSmokeTests
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF");
    private static readonly byte[] ZipBytes = [0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8];

    private async Task<int> AddCategory(string group, string title, int order = 0, bool active = true)
    {
        var response = await client.PostAsJsonAsync("/api/admin/library/categories", new LibraryCategoryRequest(group, title, order, active));
        response.EnsureSuccessStatusCode();
        return (await client.GetFromJsonAsync<List<LibraryCategoryAdminDto>>("/api/admin/library/categories"))!.Single(c => c.GroupCode == group && c.Title == title).CategoryId;
    }

    private static MultipartFormDataContent DocumentForm(int categoryId, string title, string? fileName, byte[]? bytes, string? description = null, bool active = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(categoryId.ToString()), "CategoryId" }, { new StringContent(title), "Title" },
            { new StringContent(description ?? ""), "Description" }, { new StringContent(active ? "true" : "false"), "IsActive" },
        };
        if (bytes is not null) form.Add(new ByteArrayContent(bytes) { Headers = { ContentType = new("text/html") } }, "File", fileName!);   // نوع ادعاشده‌ی کاربر نادیده گرفته می‌شود
        return form;
    }

    private async Task<long> UploadDocument(int categoryId, string title, string fileName = "doc.pdf", byte[]? bytes = null, string? description = null, bool active = true)
    {
        var response = await client.PostAsync("/api/admin/library/documents", DocumentForm(categoryId, title, fileName, bytes ?? PdfBytes, description, active));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents?search=" + Uri.EscapeDataString(title)))!.Items.Single(d => d.Title == title).DocumentId;
    }

    private HttpClient VisitorWithClientKey()
    {
        var visitor = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        visitor.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        return visitor;
    }

    private string StoredFiles() => Directory.Exists(api.LibraryDirectory) ? string.Join(",", Directory.GetFiles(api.LibraryDirectory).Select(Path.GetFileName).Order()) : "";

    [Fact]
    public async Task Library_categories_are_validated_unique_per_group_and_deleted_only_when_empty()
    {
        await Login();
        foreach (var bad in new[]
        {
            new LibraryCategoryRequest("nonsense", "عنوان"), new LibraryCategoryRequest(LibraryGroups.Forms, "  "), new LibraryCategoryRequest(LibraryGroups.Forms, new string('ع', 201)),
            new LibraryCategoryRequest(LibraryGroups.Forms, "عنوان", -1), new LibraryCategoryRequest(LibraryGroups.Forms, "عنوان", 10001),
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/library/categories", bad)).StatusCode);

        var forms = await AddCategory(LibraryGroups.Forms, "اداری");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/library/categories", new LibraryCategoryRequest(LibraryGroups.Forms, "اداری"))).StatusCode);   // تکراری در همان بخش
        var rules = await AddCategory(LibraryGroups.Rules, "اداری");                                                                                                                    // همان عنوان در بخش دیگر مجاز است
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/admin/library/categories/{forms}", new LibraryCategoryRequest(LibraryGroups.Forms, "مالی", 3, false))).StatusCode);
        var edited = (await client.GetFromJsonAsync<List<LibraryCategoryAdminDto>>("/api/admin/library/categories"))!.Single(c => c.CategoryId == forms);
        Assert.Equal(("مالی", 3, false), (edited.Title, edited.DisplayOrder, edited.IsActive));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/admin/library/categories/99999", new LibraryCategoryRequest(LibraryGroups.Forms, "x"))).StatusCode);

        await UploadDocument(rules, "سند");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/library/categories/{rules}/delete", null)).StatusCode);          // سند دارد
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/library/categories/{forms}/delete", null)).StatusCode);                  // خالی است
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/admin/library/categories/{forms}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Upload_accepts_only_allowed_files_whose_content_matches_their_extension()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Forms, "فرم‌ها");
        async Task<HttpStatusCode> Try(string? name, byte[]? bytes, int? categoryId = null, string title = "سند") =>
            (await client.PostAsync("/api/admin/library/documents", DocumentForm(categoryId ?? category, title, name, bytes))).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Try("virus.exe", PdfBytes));                                    // پسوند غیرمجاز
        Assert.Equal(HttpStatusCode.BadRequest, await Try("fake.pdf", Encoding.ASCII.GetBytes("MZ\u0090\0\u0003")));   // اجرایی با پسوند pdf
        Assert.Equal(HttpStatusCode.BadRequest, await Try("page.html", Encoding.ASCII.GetBytes("<html>")));
        Assert.Equal(HttpStatusCode.BadRequest, await Try("fake.docx", PdfBytes));                                    // امضای pdf با پسوند docx
        Assert.Equal(HttpStatusCode.BadRequest, await Try("noext", PdfBytes));
        Assert.Equal(HttpStatusCode.BadRequest, await Try("empty.pdf", []));
        Assert.Equal(HttpStatusCode.BadRequest, await Try("big.pdf", PdfBytes.Concat(new byte[1024 * 1024]).ToArray()));   // بیش از سقف ۱ مگابایت آزمون
        Assert.Equal(HttpStatusCode.BadRequest, await Try(null, null));                                                // بدون فایل
        Assert.Equal(HttpStatusCode.BadRequest, await Try("doc.pdf", PdfBytes, categoryId: 99999));                    // دسته‌ی ناموجود
        Assert.Equal(HttpStatusCode.BadRequest, await Try("doc.pdf", PdfBytes, title: " "));                           // عنوان خالی
        Assert.Equal("", StoredFiles());                                                                               // هیچ فایل ردشده‌ای روی دیسک نماند

        Assert.Equal(HttpStatusCode.OK, await Try("..\\..\\evil/Report.PDF", PdfBytes));                              // مسیر از نام فایل حذف می‌شود و پسوند بزرگ هم مجاز است
        var stored = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items.Single();
        Assert.Equal("Report.PDF", stored.FileName);
        Assert.Equal("pdf", stored.Extension);
        Assert.Equal(PdfBytes.Length, stored.SizeBytes);
        var onDisk = Directory.GetFiles(api.LibraryDirectory).Single();
        Assert.Matches("^[0-9a-f]{32}$", Path.GetFileName(onDisk));                                                    // نام دیسک تصادفی است، نه نام کاربر
        Assert.Equal(PdfBytes, await File.ReadAllBytesAsync(onDisk));
        Assert.Equal(HttpStatusCode.OK, await Try("book.docx", ZipBytes, title: "ورد"));                              // امضای zip برای docx
    }

    [Fact]
    public async Task Public_library_lists_only_active_documents_of_active_categories_with_persian_search_and_download()
    {
        await Login();
        var rules = await AddCategory(LibraryGroups.Rules, "دستورالعمل‌ها", 1);
        var forms = await AddCategory(LibraryGroups.Forms, "فرم‌های فاوا", 1);
        var hiddenCategory = await AddCategory(LibraryGroups.Forms, "دسته‌ی مخفی", 2, active: false);
        var empty = await AddCategory(LibraryGroups.Publications, "خالی");
        var shown = await UploadDocument(rules, "دستورالعمل ایمنی", "safety.pdf", description: "راهنمای ایمنی بندر");
        await UploadDocument(forms, "فرم درخواست رایانه", "request.docx", ZipBytes);
        await UploadDocument(rules, "پیش‌نویس", "draft.pdf", active: false);
        await UploadDocument(hiddenCategory, "در دسته‌ی مخفی", "hidden.pdf");

        using var visitor = VisitorWithClientKey();
        var catalog = (await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library?documents=true"))!;
        Assert.Equal(2, catalog.Total);
        Assert.Equal(["دستورالعمل ایمنی", "فرم درخواست رایانه"], catalog.Documents.Select(d => d.Title).Order(StringComparer.Ordinal));
        Assert.Equal([rules, forms], catalog.Categories.Select(c => c.CategoryId).Order());                          // دسته‌ی خالی و غیرفعال نیست
        Assert.DoesNotContain(catalog.Categories, c => c.CategoryId == empty || c.CategoryId == hiddenCategory);
        Assert.Empty((await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library"))!.Documents);          // بدون documents فقط دسته‌ها

        Assert.Equal(["دستورالعمل ایمنی"], (await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library?documents=true&q=" + Uri.EscapeDataString("دستورالعملِ"[..^1])))!.Documents.Select(d => d.Title));
        Assert.Single((await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library?documents=true&q=" + Uri.EscapeDataString("ايمني")))!.Documents);     // ی عربی
        Assert.Single((await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library?documents=true&q=" + Uri.EscapeDataString("فرم‌های")))!.Documents);   // نیم‌فاصله، از نام دسته
        Assert.Single((await visitor.GetFromJsonAsync<LibraryCatalogDto>("/api/public/library?documents=true&q=REQUEST"))!.Documents);                                  // نام فایل، بدون حساسیت به حروف
        Assert.Single((await visitor.GetFromJsonAsync<LibraryCatalogDto>($"/api/public/library?documents=true&group={LibraryGroups.Forms}"))!.Documents);
        Assert.Single((await visitor.GetFromJsonAsync<LibraryCatalogDto>($"/api/public/library?documents=true&category={rules}"))!.Documents);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.GetAsync("/api/public/library?group=hack")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.GetAsync("/api/public/library?q=" + new string('a', 101))).StatusCode);

        var download = await visitor.GetAsync($"/api/public/library/documents/{shown}/file");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);                              // نوع را سرور از پسوند تعیین کرد، نه text/html کاربر
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("safety.pdf", download.Content.Headers.ContentDisposition.FileName ?? download.Content.Headers.ContentDisposition.FileNameStar);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Inactive_missing_or_hidden_documents_cannot_be_downloaded_and_admin_toggles_take_effect_at_once()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Rules, "آیین‌نامه‌ها");
        var id = await UploadDocument(category, "آیین‌نامه");
        using var visitor = VisitorWithClientKey();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/public/library/documents/{id}/file")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/library/documents/{id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/library/documents/{id}/file")).StatusCode);   // مدیر فایل غیرفعال را می‌گیرد
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/library/documents/{id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/public/library/documents/{id}/file")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/admin/library/categories/{category}", new LibraryCategoryRequest(LibraryGroups.Rules, "آیین‌نامه‌ها", 0, false))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{id}/file")).StatusCode);   // دسته‌ی غیرفعال سندش را محو می‌کند
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/admin/library/categories/{category}", new LibraryCategoryRequest(LibraryGroups.Rules, "آیین‌نامه‌ها", 0, true))).StatusCode);

        File.Delete(Directory.GetFiles(api.LibraryDirectory).Single());                                                  // فایل از دیسک رفته
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{id}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/public/library/documents/99999/file")).StatusCode);
    }

    [Fact]
    public async Task Replacing_a_file_swaps_it_on_disk_only_after_success_and_deleting_removes_the_file()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Publications, "کتاب‌ها");
        var id = await UploadDocument(category, "کتاب", "book.pdf");
        var first = StoredFiles();
        Assert.NotEqual("", first);

        var metadataOnly = await client.PostAsync($"/api/admin/library/documents/{id}", DocumentForm(category, "کتاب ۲", null, null, "توضیح"));
        Assert.Equal(HttpStatusCode.OK, metadataOnly.StatusCode);
        Assert.Equal(first, StoredFiles());                                                                            // فایل دست نخورد
        var edited = await client.GetFromJsonAsync<LibraryDocumentAdminDto>($"/api/admin/library/documents/{id}");
        Assert.Equal(("کتاب ۲", "توضیح", "book.pdf"), (edited!.Title, edited.Description, edited.FileName));

        var bad = await client.PostAsync($"/api/admin/library/documents/{id}", DocumentForm(category, "کتاب ۲", "new.pdf", Encoding.ASCII.GetBytes("MZ-not-a-pdf")));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(first, StoredFiles());                                                                            // جایگزینی ناموفق فایل قدیمی را نمی‌برد

        var good = await client.PostAsync($"/api/admin/library/documents/{id}", DocumentForm(category, "کتاب ۲", "new.zip", ZipBytes));
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.NotEqual(first, StoredFiles());
        Assert.Single(Directory.GetFiles(api.LibraryDirectory));                                                       // قدیمی پاک شد
        Assert.Equal("zip", (await client.GetFromJsonAsync<LibraryDocumentAdminDto>($"/api/admin/library/documents/{id}"))!.Extension);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/library/documents/{id}/delete", null)).StatusCode);
        Assert.Equal("", StoredFiles());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/library/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/admin/library/documents/{id}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Library_management_needs_LIBRARY_EDIT_checked_live_while_public_reading_needs_nothing()
    {
        GrantMember("LIBRARY_NO_EDIT", P.IntranetAdmin, P.UserView);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/library/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/library/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/library/documents", DocumentForm(1, "x", "a.pdf", PdfBytes))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/library/documents/1/delete", null)).StatusCode);
        GrantMember("LIBRARY_EDITOR", P.LibraryEdit);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/library/categories")).StatusCode);        // بدون ورود دوباره
        using var visitor = VisitorWithClientKey();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync("/api/public/library")).StatusCode);
        Assert.Contains(P.All, p => p.Code == P.LibraryEdit);
    }

    [Fact]
    public async Task Portal_library_pages_show_categories_documents_search_and_download_for_anonymous_visitors()
    {
        await Login();
        var rules = await AddCategory(LibraryGroups.Rules, "دستورالعمل‌ها");
        var forms = await AddCategory(LibraryGroups.Forms, "فرم‌های فاوا");
        var docId = await UploadDocument(rules, "دستورالعمل ایمنی", "safety.pdf", description: "راهنمای ایمنی بندر");
        await UploadDocument(forms, "فرم رایانه", "pc.docx", ZipBytes);

        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var overview = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library"));
        Assert.Contains("pp-site", overview);
        Assert.Contains("دستورالعمل‌ها", overview);
        Assert.Contains($"href=\"/Library?group=rules&category={rules}\"", overview);                                      // کارت دسته به فهرست سندها می‌رود
        Assert.DoesNotContain("دستورالعمل ایمنی", overview);                                                                // نمای کلی فقط دسته‌ها را نشان می‌دهد
        foreach (var g in LibraryGroups.All) Assert.Contains($"href=\"/Library?group={g.Code}\"", overview);

        var group = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?group=rules"));
        Assert.Contains("دستورالعمل ایمنی", group);
        Assert.Contains("راهنمای ایمنی بندر", group);
        Assert.DoesNotContain("فرم رایانه", group);
        Assert.Contains($"href=\"/Library/Download/{docId}\"", group);

        var category = WebUtility.HtmlDecode(await anonymous.GetStringAsync($"/Library?group=forms&category={forms}"));
        Assert.Contains("فرم رایانه", category);
        var search = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?q=" + Uri.EscapeDataString("ايمني")));
        Assert.Contains("دستورالعمل ایمنی", search);
        Assert.Contains("پاک‌کردن فیلتر", search);
        Assert.Contains("موردی مطابق جست‌وجوی شما یافت نشد", WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?q=zzzz")));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/Library?group=unknown&category=999&q=" + new string('a', 500))).StatusCode);   // ورودی نامعتبر = پیش‌فرض

        var file = await anonymous.GetAsync($"/Library/Download/{docId}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(PdfBytes, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", file.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("nosniff", file.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/Library/Download/99999")).StatusCode);

        var home = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Portal"));
        var section = home[home.IndexOf("id=\"quicklinks\"", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("</section>", StringComparison.Ordinal)];
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(section, "class=\"ql-all\""));                         // یک دکمه برای کل بخش
        Assert.Contains("class=\"ql-all\" href=\"/Library\"", section);
        Assert.Contains($"href=\"/Library?group=rules&category={rules}\"", section);                                      // هر دسته به نتیجه‌ی خودش
    }

    [Fact]
    public async Task Portal_library_shows_a_clear_empty_state_before_any_document_is_published()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Contains("هنوز سندی منتشر نشده است", WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library")));
        Assert.Contains("هنوز سندی منتشر نشده است", WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Portal")));
    }

    [Fact]
    public async Task Admin_library_pages_upload_a_document_list_it_and_manage_categories()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Forms, "فرم‌های فاوا");
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            Assert.Contains("هنوز سندی ثبت نشده است", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library")));
            var form = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library/Edit"));
            Assert.Contains("name=\"Files\"", form);
            Assert.Contains("فرم‌های فاوا", form);
            Assert.Contains(".pdf,.doc,.docx", form);

            var token = await Antiforgery(browser, "/Admin/Library/Edit");
            var upload = new MultipartFormDataContent
            {
                { new StringContent(category.ToString()), "CategoryId" }, { new StringContent("فرم تعمیر"), "Title" }, { new StringContent("برای درخواست تعمیر"), "Description" },
                { new StringContent("true"), "IsActive" }, { new StringContent(token), "__RequestVerificationToken" },
                { new ByteArrayContent(PdfBytes), "Files", "repair.pdf" },
            };
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Admin/Library/Edit", upload)).StatusCode);
            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library"));
            Assert.Contains("فرم تعمیر", list);
            Assert.Contains("PDF", list);

            var rejected = await browser.PostAsync("/Admin/Library/Edit", new MultipartFormDataContent
            {
                { new StringContent(category.ToString()), "CategoryId" }, { new StringContent("بد"), "Title" }, { new StringContent(token), "__RequestVerificationToken" },
                { new ByteArrayContent(Encoding.ASCII.GetBytes("MZ-binary")), "Files", "bad.pdf" },
            });
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);                                                           // صفحه با پیام خطا، نه ریدایرکت
            Assert.Contains("محتوای فایل با پسوند آن نمی‌خواند", WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync()));

            var id = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items.Single().DocumentId;
            var edit = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Library/Edit?id={id}"));
            Assert.Contains("repair.pdf", edit);
            var download = await browser.GetAsync($"/Admin/Library/Edit?id={id}&handler=Download");
            Assert.Equal(PdfBytes, await download.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Admin/Library/Edit?id=99999")).StatusCode);

            var categories = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library/Categories"));
            Assert.Contains("فرم‌های فاوا", categories);
            Assert.Contains("دسته‌ی تازه", categories);
            var created = await browser.PostAsync("/Admin/Library/Categories?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["GroupCode"] = "rules", ["Title"] = "آیین‌نامه‌ها", ["DisplayOrder"] = "2", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.Contains("آیین‌نامه‌ها", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library/Categories")));

            var removed = await browser.PostAsync($"/Admin/Library?handler=Delete&id={id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
            Assert.Equal("", StoredFiles());
        }
    }
}
