using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalFinance.Shared.Constants;
using PersonalFinance.Shared.Contracts;
using PersonalFinance.Shared.DTOs.Todos;
using PersonalFinance.Web.Controllers;
using PersonalFinance.Web.Infrastructure;
using PersonalFinance.Web.Models;

namespace PersonalFinance.Tests.Web;

/// <summary>
/// The Web To-do List page: signed-in only, shows the Account's open To-dos, and adds a To-do through
/// <see cref="ITodoApi"/>, answering the page's JavaScript with the refreshed list.
/// </summary>
public class TodosControllerTests
{
    [Fact]
    public void TodosController_RequiresASignedInAccount()
    {
        Assert.NotNull(typeof(TodosController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task Index_ShowsTheOpenTodosInTheOrderApiServiceReturns()
    {
        var (api, proxy) = ApiProxy.Create<ITodoApi>();
        proxy.Returns(nameof(ITodoApi.GetOpenTodosAsync), new List<TodoItemDto>
        {
            new() { Id = 2, Title = "Pay rent" }, new() { Id = 1, Title = "Call the bank" }
        });

        var result = await WithContext(new TodosController(api, NullLogger<TodosController>.Instance)).Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TodoListViewModel>(view.Model);
        Assert.Equal([2, 1], model.Todos.Select(t => t.Id));
    }

    [Fact]
    public async Task Create_AddsTheTodo_AndReturnsTheRefreshedList()
    {
        var (api, proxy) = ApiProxy.Create<ITodoApi>();
        proxy.Returns(nameof(ITodoApi.CreateTodoAsync), new TodoItemDto { Id = 7, Title = "Pay rent" });
        proxy.Returns(nameof(ITodoApi.GetOpenTodosAsync), new List<TodoItemDto> { new() { Id = 7, Title = "Pay rent" } });

        var result = await WithContext(new TodosController(api, NullLogger<TodosController>.Instance))
            .Create(new CreateTodoRequest { Title = "Pay rent" });

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(7, Assert.Single(Assert.IsType<List<TodoItemDto>>(json.Value)).Id);
        Assert.Equal([nameof(ITodoApi.CreateTodoAsync), nameof(ITodoApi.GetOpenTodosAsync)], proxy.Calls);
    }

    [Fact]
    public async Task Create_WithAnUnreadableBody_Returns400WithoutCallingApiService()
    {
        var (api, proxy) = ApiProxy.Create<ITodoApi>();

        var result = await WithContext(new TodosController(api, NullLogger<TodosController>.Instance)).Create(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(TodoMessages.InvalidRequest, badRequest.Value!.ToString());
        Assert.Empty(proxy.Calls);
    }

    [Fact]
    public void Create_RequiresTheAntiforgeryToken()
    {
        Assert.NotNull(typeof(TodosController).GetMethod(nameof(TodosController.Create))!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task Delete_DeletesTheTodo_AndReturnsTheRefreshedList()
    {
        var (api, proxy) = ApiProxy.Create<ITodoApi>();
        proxy.Returns(nameof(ITodoApi.DeleteTodoAsync), null);
        proxy.Returns(nameof(ITodoApi.GetOpenTodosAsync), new List<TodoItemDto> { new() { Id = 8, Title = "Keep me" } });

        var result = await WithContext(new TodosController(api, NullLogger<TodosController>.Instance)).Delete(7);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Equal(8, Assert.Single(Assert.IsType<List<TodoItemDto>>(json.Value)).Id);
        Assert.Equal([nameof(ITodoApi.DeleteTodoAsync), nameof(ITodoApi.GetOpenTodosAsync)], proxy.Calls);
    }

    [Fact]
    public async Task Delete_WhenApiServiceFails_LetsJsonApiErrorsReportIt_WithoutReloadingTheList()
    {
        var (api, proxy) = ApiProxy.Create<ITodoApi>();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            WithContext(new TodosController(api, NullLogger<TodosController>.Instance)).Delete(7));

        Assert.Equal([nameof(ITodoApi.DeleteTodoAsync)], proxy.Calls);
        Assert.NotNull(typeof(TodosController).GetCustomAttribute<JsonApiErrorsAttribute>());
    }

    [Fact]
    public void Delete_IsAPost_ThatRequiresTheAntiforgeryToken()
    {
        var method = typeof(TodosController).GetMethod(nameof(TodosController.Delete))!;
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private static TodosController WithContext(TodosController controller)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, "test-user-123") }, "TestAuthType"))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
