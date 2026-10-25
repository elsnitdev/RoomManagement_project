using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using RoomManagement.Business.DTOs.Users;
using RoomManagement.Business.Exceptions;
using RoomManagement.Business.Services;
using RoomManagement.Business.Validators;
using RoomManagement.Domain;
using RoomManagement.Domain.Entities;

namespace RoomManagement.Tests;

public sealed class BusinessRulesTests
{
    [Fact]
    public async Task Both_creation_services_reject_overlong_email_before_storage()
    {
        var store = new TestUserStore();
        var service = new UserService(store, new PasswordHasher<AppUser>(), NullLogger<UserService>.Instance);
        var email = new string('a', 250) + "@example.com";
        await Assert.ThrowsAsync<AppValidationException>(() => service.CreateUserByAdminAsync(new CreateUserDto
        {
            UserName = "valid-user", Email = email, Password = TestUserStore.Password, Role = UserRoles.Tenant
        }));
        await Assert.ThrowsAsync<AppValidationException>(() => service.RegisterLandlordAsync(new RegisterLandlordDto
        {
            UserName = "valid-user", Email = email, Password = TestUserStore.Password
        }));
        Assert.Empty(store.Users);
    }

    [Fact]
    public void Email_rule_accepts_database_boundary_after_trimming()
    {
        var email = new string('a', 244) + "@example.com";
        Assert.Equal(256, email.Length);
        UserAccountRules.EnsureValidEmail(" " + email + " ");
        Assert.Throws<AppValidationException>(() => UserAccountRules.EnsureValidEmail("a" + email));
    }

    [Fact]
    public void Business_assembly_has_no_persistence_dependency()
    {
        var references = typeof(UserService).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference => reference.Name == "RoomManagement.Data" ||
            reference.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }
}
