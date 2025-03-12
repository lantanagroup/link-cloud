using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;
using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;

namespace LantanaGroup.Link.LinkAdmin.BFF.Presentation.Endpoints.System.Hanlders;

public static class GetSystemHealth
{
    public static async Task<IResult> Handle(HttpContext context, 
        AccountService accountService, 
        MeasureEvalService measureEvalService)
    {
        var accountServiceHealth = await accountService.ServiceHealthCheck(context.RequestAborted);
        //var measureEvalServiceHealth = await measureEvalService.ServiceHealthCheck(context.RequestAborted);
        
        List<LinkServiceHealthReport> results = [];
        var accountResult = await accountServiceHealth.Content.ReadFromJsonAsync<LinkServiceHealthReport>(cancellationToken: context.RequestAborted);
        accountResult.Service = "Account";
        
        results.Add(accountResult);
        
        
        

        return Results.Ok(results);
    }
}