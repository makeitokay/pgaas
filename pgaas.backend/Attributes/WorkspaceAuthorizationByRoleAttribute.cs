using System.Security.Claims;
using Core.Entities;
using Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace pgaas.backend.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class WorkspaceAuthorizationByRoleAttribute : Attribute, IAsyncActionFilter

{
	public Role RequiredRole { get; }

	public WorkspaceAuthorizationByRoleAttribute(Role requiredRole)
	{
		RequiredRole = requiredRole;
	}

	public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
	{
		var userEmail = context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value;
		if (userEmail == null)
		{
			context.Result = new UnauthorizedResult();
			return;
		}

		if (!context.RouteData.Values.TryGetValue("workspaceId", out var workspaceIdObj)
		    || workspaceIdObj is not int workspaceId)
		{
			context.Result = new BadRequestObjectResult("Workspace ID is missing or invalid.");
			return;
		}

		var repository = context.HttpContext.RequestServices.GetRequiredService<IRepository<WorkspaceUser>>();
		var hasPermission = await repository.Items.AnyAsync(wu =>
			wu.User.Email == userEmail && wu.WorkspaceId == workspaceId && wu.Role >= RequiredRole);

		if (!hasPermission)
		{
			context.Result = new ForbidResult();
			return;
		}

		await next();
	}

}