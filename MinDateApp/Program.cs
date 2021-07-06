using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using TimeZoneConverter;

var builder = WebApplication.CreateBuilder(args);
await using var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

RequestDelegate getNowHandler = async (context) =>
{
    var containsTimezone = ContainsHeader(context, "tz");
    var response = !containsTimezone ?
        GetResponseForCommonTimeZones() :
        GetResponseForSpecificTimeZones(context);

    await context.Response.WriteAsJsonAsync(response);
};

RequestDelegate helloDelegate = async (context) =>
{
    var response = new
    {
        Message = "Hello World",
        Endpoints = new
        {
            Now = "/now",
            TimeZones = "/timezone"
        }
    };

    await context.Response.WriteAsJsonAsync(response);
};

RequestDelegate getTimeZonesDelegate = async (context) =>
{
    var containSearch = ContainsHeader(context, "q");
    var response = !containSearch ?
        GetAllTimeZoneResponses() :
        FilterTimeZoneResponses(context);

    await context.Response.WriteAsJsonAsync(response);
};



bool ContainsHeader(HttpContext httpContext, string key)
{
    return httpContext.Request.Query.ContainsKey(key);
}

IEnumerable<NowResponse> GetResponseForSpecificTimeZones(HttpContext httpContext)
{
    var utcNow = DateTimeOffset.UtcNow;
    var tzQuery = httpContext.Request.Query["tz"].ToString();

    var tzQueryValues = tzQuery.Split(",", StringSplitOptions.TrimEntries);

    var responses = tzQueryValues.Select(term =>
    {
        var searchedTimeZone = TimeZoneInfo.FindSystemTimeZoneById(term);
        var specificTimeZone = new NowResponse(searchedTimeZone, utcNow)
        {
            
        };
        return specificTimeZone;
    }).ToList();

    responses.Insert(0, new LocalNowResponse(utcNow));
    responses.Insert(1, new UtcNowResponse(utcNow));
    return responses;
}

IEnumerable<NowResponse> GetResponseForCommonTimeZones()
{
    var utcNow = DateTimeOffset.UtcNow;
    var commonTimeZones = new[]
        {"Asia/Bangkok", "Asia/Singapore", "Australia/Melbourne", "Asia/Shanghai", "Africa/Johannesburg"};

    var commonTimeZoneResponses = commonTimeZones
        .Select(tz => TZConvert.GetTimeZoneInfo(tz))
        .Select(tz => new NowResponse(tz, utcNow));

    var nowResponses = new List<NowResponse>
    {
        new LocalNowResponse(utcNow),
        new UtcNowResponse(utcNow)
    };

    return commonTimeZoneResponses.Concat(nowResponses);
}

IEnumerable<TimeZoneResponse> GetAllTimeZoneResponses()
{
    var timeZoneInfos = TimeZoneInfo.GetSystemTimeZones();

    return timeZoneInfos.Select(tz => new TimeZoneResponse(tz));
}

IEnumerable<TimeZoneResponse> FilterTimeZoneResponses(HttpContext httpContext)
{
    var timeZoneInfos = TimeZoneInfo.GetSystemTimeZones();
    var searchTerm = httpContext.Request.Query["q"].ToString();

    return timeZoneInfos
        .Where(tz => tz.Id.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase) ||
                                tz.DisplayName.Contains(searchTerm, StringComparison.InvariantCultureIgnoreCase))
        .Select(tz => new TimeZoneResponse(tz));
}

app.MapGet("/", helloDelegate);

app.MapGet("/now", getNowHandler);

app.MapGet("/timezone", getTimeZonesDelegate);

await app.RunAsync();

class NowResponse
{
    public NowResponse(TimeZoneInfo tz, DateTimeOffset now)
    {
        Name = tz.DisplayName;
        Id = tz.Id;
        Now = TimeZoneInfo.ConvertTime(now, tz);
    }

    public string Name { get; set; }

    public string Id { get; set; }

    public DateTimeOffset Now { get; set; }
}

class TimeZoneResponse
{
    public TimeZoneResponse(TimeZoneInfo tz)
    {
        Id = tz.Id;
        Name = tz.DisplayName;
        StandardName = tz.StandardName;
        UtcOffset = tz.BaseUtcOffset.Hours;
    }

    public TimeZoneResponse()
    {

    }

    public string Id { get; set; }

    public string Name { get; set; }

    public string StandardName { get; set; }

    public int UtcOffset { get; set; }

}

class UtcNowResponse : NowResponse
{
    public UtcNowResponse(DateTimeOffset now) : base(TimeZoneInfo.Utc, now)
    {
    }
}

class LocalNowResponse : NowResponse
{
    public LocalNowResponse(DateTimeOffset now) : base(TimeZoneInfo.Local, now)
    {
    }
}
