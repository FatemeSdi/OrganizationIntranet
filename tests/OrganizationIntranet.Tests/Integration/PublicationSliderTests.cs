extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// ترتیب و انتخاب مطالب اسلایدر صفحه‌ی اصلی (migration 016): فقط مطلب منتشرشده، به ترتیب جایگاه و سپس تازگی.
public sealed partial class PortalSmokeTests
{
    private async Task<long> Publish(string title, bool slider = true, int order = 0, bool published = true, bool isPublic = true)
    {
        var response = await client.PostAsJsonAsync("/api/admin/publications", new PublicationEditRequest
        { Title = title, Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = published, IsPublic = isPublic && published, ShowInSlider = slider, SlideOrder = order });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PublicationDto>())!.PublicationId;
    }

    private async Task<List<string>> SliderTitles(bool signedIn)
    {
        if (!signedIn)
        {
            using var anonymous = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            anonymous.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
            var response = await anonymous.GetAsync("/api/public/publications?slider=true");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<PublicationListDto>())!.Items.Select(p => p.Title).ToList();
        }
        await Login("member");
        return (await client.GetFromJsonAsync<PublicationListDto>("/api/publications?slider=true"))!.Items.Select(p => p.Title).ToList();
    }

    [Fact]
    public async Task Slider_orders_by_position_then_newest_and_zero_means_no_priority()
    {
        await Login();
        await Publish("قدیمی بدون اولویت");
        await Publish("دوم", order: 2);
        await Publish("تازه بدون اولویت");
        await Publish("اول", order: 1);

        var titles = await SliderTitles(signedIn: true);
        Assert.Equal(["اول", "دوم", "تازه بدون اولویت", "قدیمی بدون اولویت"], titles);                          // ۱، ۲، سپس بدون اولویت‌ها تازه‌تر جلوتر
        var list = (await client.GetFromJsonAsync<PublicationListDto>("/api/publications"))!.Items.Select(p => p.Title).ToList();
        Assert.Equal("اول", list[0]);                                                                              // فهرست عادی (بدون slider) هنوز بر اساس تازگی است
    }

    [Fact]
    public async Task Slider_shows_only_published_items_that_are_allowed_in_it_and_never_drafts_or_archived()
    {
        await Login();
        await Publish("در اسلایدر", order: 1);
        await Publish("خارج از اسلایدر", slider: false);
        await Publish("پیش‌نویس", published: false);
        var archived = await Publish("بایگانی");
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/publications/{archived}/archive", null)).StatusCode);

        Assert.Equal(["در اسلایدر"], await SliderTitles(signedIn: true));
        Assert.Equal(["در اسلایدر"], await SliderTitles(signedIn: false));
        await Login();
        var all = (await client.GetFromJsonAsync<PublicationListDto>("/api/publications"))!.Items.Select(p => p.Title).Order().ToList();
        Assert.Contains("خارج از اسلایدر", all);                                                                   // فقط از اسلایدر کنار رفته، از فهرست نه
    }

    [Fact]
    public async Task Slider_settings_are_saved_validated_and_returned_for_editing()
    {
        await Login();
        var id = await Publish("اسلاید", order: 7);
        var saved = (await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{id}"))!;
        Assert.True(saved.ShowInSlider);
        Assert.Equal(7, saved.SlideOrder);

        var bad = await client.PostAsJsonAsync($"/api/admin/publications/{id}", new PublicationEditRequest
        { Title = "اسلاید", Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = true, ShowInSlider = true, SlideOrder = 10001, Revision = saved.Revision });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var negative = await client.PostAsJsonAsync($"/api/admin/publications/{id}", new PublicationEditRequest
        { Title = "اسلاید", Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = true, ShowInSlider = true, SlideOrder = -1, Revision = saved.Revision });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(7, (await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{id}"))!.SlideOrder);   // ردشده‌ها چیزی را عوض نکردند
    }

    [Fact]
    public async Task Admin_form_has_the_slider_fields_and_the_list_marks_slider_items()
    {
        await Login();
        var id = await Publish("نمایش در اسلایدر", order: 3);
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var form = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Publications/Edit?id={id}"));
            Assert.Contains("نمایش در اسلایدر صفحه‌ی اصلی", form);
            Assert.Contains("name=\"Input.SlideOrder\"", form);
            Assert.Contains("value=\"3\"", form);
            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications"));
            Assert.Contains("اسلایدر · 3", list);
        }
    }
}
