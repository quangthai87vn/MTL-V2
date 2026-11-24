using MTL.Domain.Identity;
using Xunit;

namespace MTL.Domain.Tests;

public sealed class PermissionsTests
{
    [Fact]
    public void Permission_codes_are_unique_and_namespaced()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Permissions.All, permission =>
        {
            Assert.Contains('.', permission);
            Assert.Equal(permission, permission.ToLowerInvariant());
        });
    }

    [Fact]
    public void Core_operational_permissions_are_defined()
    {
        Assert.Contains(Permissions.ProductsManage, Permissions.All);
        Assert.Contains(Permissions.SalesManage, Permissions.All);
        Assert.Contains(Permissions.PurchasesManage, Permissions.All);
        Assert.Contains(Permissions.InventoryManage, Permissions.All);
        Assert.Contains(Permissions.SettingsManage, Permissions.All);
    }
}

