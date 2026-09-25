namespace Atlantic.Pedidos.Domain.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string> { Admin, User };
}
