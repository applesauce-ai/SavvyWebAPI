using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Savvy.Application.Auth;
using Savvy.Application.PaymentRuns;
using Savvy.Application.Shifts;
using Savvy.Application.Timesheets;
using Savvy.Domain.Entities;

namespace Savvy.Application;

/// <summary>Registers application-layer use-case services.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<IPaymentRunService, PaymentRunService>();
        services.AddScoped<IAuthService, AuthService>();

        // PBKDF2 password hasher (same algorithm used to seed users).
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

        return services;
    }
}
