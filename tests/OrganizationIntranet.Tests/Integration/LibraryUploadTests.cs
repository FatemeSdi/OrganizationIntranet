extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// آپلود چندتایی، پیش‌نمایش تصویر، پسوندهای tif و rar و وارد کردن فایل‌های موجود به کتابخانه‌ی اسناد.
public sealed partial class PortalSmokeTests
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 73, 72, 68, 82];
    private static readonly byte[] JpgBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70, 0, 1];
    private static readonly byte[] TifBytes = [0x49, 0x49, 0x2A, 0x00, 8, 0, 0, 0, 1, 2, 3, 4];
    private static readonly byte[] RarBytes = [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00, 1, 2, 3, 4];

    private static MultipartFormDataContent BatchForm(int categoryId, params (string Name, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent { { new StringContent(categoryId.ToString()), "CategoryId" }, { new StringContent("توضیح مشترک"), "Description" }, { new StringContent("true"), "IsActive" } };
        foreach (var (name, bytes) in files) form.Add(new ByteArrayContent(bytes), "Files", name);
        return form;
    }

    [Fact]
    public async Task Batch_upload_creates_one_document_per_file_titled_by_file_name_and_rejects_only_the_bad_ones()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Forms, "فرم‌ها");
        var response = await client.PostAsync("/api/admin/library/documents/batch", BatchForm(category,
            ("فرم مرخصی.pdf", PdfBytes), ("scan.PNG", PngBytes), ("book.docx", ZipBytes), ("virus.exe", PdfBytes), ("fake.pdf", Encoding.ASCII.GetBytes("MZ-binary")), ("empty.pdf", [])));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<LibraryBatchResultDto>())!;
        Assert.Equal(3, result.Created);
        Assert.Equal(["empty.pdf", "fake.pdf", "virus.exe"], result.Failures.Select(f => f.FileName).Order());
        Assert.All(result.Failures, f => Assert.NotEmpty(f.Reason));

        var documents = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items;
        Assert.Equal(["book", "scan", "فرم مرخصی"], documents.Select(d => d.Title).Order(StringComparer.Ordinal));      // عنوان = نام فایل بدون پسوند
        Assert.All(documents, d => { Assert.Equal("توضیح مشترک", d.Description); Assert.True(d.IsActive); Assert.Equal(category, d.CategoryId); });
        Assert.Equal(3, Directory.GetFiles(api.LibraryDirectory).Length);                                              // فایل‌های ردشده روی دیسک نماندند
    }

    [Fact]
    public async Task Batch_upload_validates_the_request_as_a_whole_and_needs_the_library_permission()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Forms, "فرم‌ها");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/library/documents/batch", BatchForm(category))).StatusCode);                  // بدون فایل
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/library/documents/batch", BatchForm(99999, ("a.pdf", PdfBytes)))).StatusCode);   // دسته‌ی ناموجود
        var tooMany = Enumerable.Range(0, LibraryRules.MaxBatchFiles + 1).Select(i => ($"f{i}.pdf", PdfBytes)).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/library/documents/batch", BatchForm(category, tooMany))).StatusCode);          // بیش از حد مجاز
        Assert.Equal("", StoredFiles());

        GrantMember("BATCH_NO_EDIT", P.IntranetAdmin, P.UserView);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/library/documents/batch", BatchForm(category, ("a.pdf", PdfBytes)))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/library/documents/1/preview")).StatusCode);
    }

    [Theory]
    [InlineData("scan.tif", "image/tiff")]
    [InlineData("scan.TIFF", "image/tiff")]
    [InlineData("archive.rar", "application/vnd.rar")]
    public async Task Tif_and_rar_files_are_accepted_only_with_their_real_signature_and_always_download_as_attachments(string name, string contentType)
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Rules, "اسکن‌ها");
        var bytes = name.EndsWith("rar", StringComparison.OrdinalIgnoreCase) ? RarBytes : TifBytes;
        var wrong = name.EndsWith("rar", StringComparison.OrdinalIgnoreCase) ? TifBytes : RarBytes;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/library/documents", DocumentForm(category, "جعلی", name, wrong))).StatusCode);   // امضای نادرست
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/admin/library/documents", DocumentForm(category, "جعلی", name, PdfBytes))).StatusCode);
        var id = await UploadDocument(category, "سند اسکن", name, bytes);

        using var visitor = VisitorWithClientKey();
        var download = await visitor.GetAsync($"/api/public/library/documents/{id}/file");
        Assert.Equal(contentType, download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{id}/preview")).StatusCode);   // tif و rar پیش‌نمایش ندارند
    }

    [Fact]
    public async Task Only_small_png_and_jpg_images_have_an_inline_preview_with_safe_headers()
    {
        Assert.True(LibraryRules.CanPreview("png", 1));
        Assert.True(LibraryRules.CanPreview("JPG", LibraryRules.MaxPreviewBytes));
        Assert.False(LibraryRules.CanPreview("jpeg", LibraryRules.MaxPreviewBytes + 1));      // بزرگ‌تر از ۳ مگابایت
        Assert.False(LibraryRules.CanPreview("tif", 100));
        Assert.False(LibraryRules.CanPreview("pdf", 100));
        Assert.False(LibraryRules.CanPreview("png", 0));

        await Login();
        var category = await AddCategory(LibraryGroups.Publications, "آموزش");
        var png = await UploadDocument(category, "تصویر", "pic.png", PngBytes);
        var jpg = await UploadDocument(category, "عکس", "photo.jpeg", JpgBytes);
        var pdf = await UploadDocument(category, "متن", "text.pdf");
        var documents = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items.ToDictionary(d => d.DocumentId);
        Assert.True(documents[png].HasPreview);
        Assert.True(documents[jpg].HasPreview);
        Assert.False(documents[pdf].HasPreview);

        using var visitor = VisitorWithClientKey();
        var preview = await visitor.GetAsync($"/api/public/library/documents/{png}/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(PngBytes, await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        Assert.Null(preview.Content.Headers.ContentDisposition);                                                         // درون‌خط، نه دانلود
        Assert.Equal("nosniff", preview.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", preview.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("image/jpeg", (await visitor.GetAsync($"/api/public/library/documents/{jpg}/preview")).Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{pdf}/preview")).StatusCode);   // غیرتصویر
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/api/public/library/documents/99999/preview")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/library/documents/{png}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/library/documents/{png}/preview")).StatusCode);      // غیرفعال برای عموم نیست
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/library/documents/{png}/preview")).StatusCode);              // ولی مدیر می‌بیند
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/library/documents/{pdf}/preview")).StatusCode);
    }

    [Fact]
    public async Task Admin_new_document_page_takes_many_files_and_lists_thumbnails_for_images()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Forms, "فرم‌های فاوا");
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var page = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library/Edit"));
            Assert.Contains("سند جدید", page);
            Assert.DoesNotContain("سند تازه", page);
            Assert.Matches("<input[^>]*name=\"Files\"[^>]*multiple|<input[^>]*multiple[^>]*name=\"Files\"", page);
            Assert.Contains(".tif", page);
            Assert.Contains(".rar", page);
            Assert.Contains("سند جدید", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library")));

            var token = await Antiforgery(browser, "/Admin/Library/Edit");
            MultipartFormDataContent Form(params (string Name, byte[] Bytes)[] files)
            {
                var form = new MultipartFormDataContent { { new StringContent(category.ToString()), "CategoryId" }, { new StringContent("true"), "IsActive" }, { new StringContent(token), "__RequestVerificationToken" } };
                foreach (var (name, bytes) in files) form.Add(new ByteArrayContent(bytes), "Files", name);
                return form;
            }
            var many = await browser.PostAsync("/Admin/Library/Edit", Form(("اول.pdf", PdfBytes), ("دوم.png", PngBytes), ("بد.exe", PdfBytes)));
            Assert.Equal(HttpStatusCode.Redirect, many.StatusCode);                                                         // دو تا ثبت شد و یکی رد
            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library"));
            Assert.Contains("اول", list);
            Assert.Contains("دوم", list);
            Assert.Contains("فایل رد شد", list);                                                                            // پیام ردشده‌ها بعد از ریدایرکت
            Assert.Contains("بد.exe", list);
            Assert.Contains("handler=Preview", list);                                                                       // بندانگشتی فقط برای تصویر

            var single = Form(("x.pdf", PdfBytes)); single.Add(new StringContent("عنوان دلخواه"), "Title");
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Admin/Library/Edit", single)).StatusCode);
            Assert.Contains("عنوان دلخواه", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Library")));          // یک فایل + عنوان = همان عنوان

            var none = await browser.PostAsync("/Admin/Library/Edit", Form(("only-bad.exe", PdfBytes)));
            Assert.Equal(HttpStatusCode.OK, none.StatusCode);                                                                // همه رد شد: صفحه با خطا
            Assert.Contains("هیچ سندی ثبت نشد", WebUtility.HtmlDecode(await none.Content.ReadAsStringAsync()));

            var id = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents?search=" + Uri.EscapeDataString("دوم")))!.Items.Single().DocumentId;
            var preview = await browser.GetAsync($"/Admin/Library/Edit?id={id}&handler=Preview");
            Assert.Equal(PngBytes, await preview.Content.ReadAsByteArrayAsync());
            Assert.Contains("handler=Preview", WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Library/Edit?id={id}")));
        }
    }

    [Fact]
    public async Task Portal_shows_a_thumbnail_for_image_documents_and_serves_only_image_previews()
    {
        await Login();
        var category = await AddCategory(LibraryGroups.Publications, "آموزش ارسال فکس");
        var image = await UploadDocument(category, "تصویر آموزش", "fax.jpg", JpgBytes);
        var pdf = await UploadDocument(category, "متن آموزش", "fax.pdf");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var html = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?group=publications"));
        Assert.Contains($"src=\"/Library/Preview/{image}\"", html);
        Assert.DoesNotContain($"/Library/Preview/{pdf}", html);                                                              // PDF نشان نوع می‌گیرد، نه تصویر
        Assert.Contains("pp-lib-ext", html);

        var preview = await anonymous.GetAsync($"/Library/Preview/{image}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(JpgBytes, await preview.Content.ReadAsByteArrayAsync());
        Assert.Null(preview.Content.Headers.ContentDisposition);
        Assert.Equal("nosniff", preview.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/Library/Preview/{pdf}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/Library/Preview/99999")).StatusCode);
    }

    [Fact]
    public async Task Library_import_command_is_a_dry_run_by_default_idempotent_and_never_leaves_its_source_folder()
    {
        await Login();
        var rules = await AddCategory(LibraryGroups.Rules, "اداری و استخدامی");
        var source = Path.Combine(Path.GetTempPath(), "intranet-import-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(source, "10.pdf"), PdfBytes);
            await File.WriteAllBytesAsync(Path.Combine(source, "11.png"), PngBytes);
            await File.WriteAllBytesAsync(Path.Combine(source, "bad.pdf"), Encoding.ASCII.GetBytes("MZ-binary"));
            var manifest = Path.Combine(source, "manifest.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[]
            {
                new { file = "10.pdf", group = "rules", category = "اداری و استخدامی", title = "مصوبه", description = "نمونه" },
                new { file = "11.png", group = "rules", category = "اداری و استخدامی", title = "تصویر", description = (string?)null },
            }));
            string[] Args(params string[] extra) => ["--import-library", manifest, "--base", source, .. extra];
            int Run(params string[] extra) => OrganizationIntranet.Api.LibraryImportCommand.RunAsync(api.Services, Args(extra)).GetAwaiter().GetResult();

            Assert.Equal(0, Run());                                                                                        // آزمایشی
            Assert.Equal("", StoredFiles());
            Assert.Empty((await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items);

            Assert.Equal(0, Run("--apply"));
            var items = (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Items;
            Assert.Equal(["تصویر", "مصوبه"], items.Select(d => d.Title).Order(StringComparer.Ordinal));
            Assert.All(items, d => { Assert.Equal(rules, d.CategoryId); Assert.True(d.IsActive); });
            Assert.Equal(2, Directory.GetFiles(api.LibraryDirectory).Length);
            Assert.True(File.Exists(Path.Combine(source, "10.pdf")));                                                      // مبدأ دست‌نخورده

            Assert.Equal(0, Run("--apply"));                                                                               // تکرار چیزی نمی‌سازد
            Assert.Equal(2, (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Total);
            Assert.Equal(2, Directory.GetFiles(api.LibraryDirectory).Length);

            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[]
            {
                new { file = "bad.pdf", group = "rules", category = "اداری و استخدامی", title = "جعلی", description = (string?)null },                // امضای نادرست
                new { file = "..\\..\\outside.pdf", group = "rules", category = "اداری و استخدامی", title = "بیرون", description = (string?)null },     // خارج از پوشه‌ی مبدأ
                new { file = "10.pdf", group = "rules", category = "دسته‌ی ناموجود", title = "بی‌دسته", description = (string?)null },
                new { file = "missing.pdf", group = "rules", category = "اداری و استخدامی", title = "ناموجود", description = (string?)null },
            }));
            Assert.Equal(1, Run("--apply"));                                                                               // ردشده‌ها: کد خروج ۱
            Assert.Equal(2, (await client.GetFromJsonAsync<LibraryDocumentPageDto>("/api/admin/library/documents"))!.Total);
        }
        finally { Directory.Delete(source, true); }
    }
}
