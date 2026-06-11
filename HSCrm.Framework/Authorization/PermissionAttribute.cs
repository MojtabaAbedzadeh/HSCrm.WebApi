using Microsoft.AspNetCore.Authorization;

namespace Framework.Authorization;

public class PermissionAttribute : AuthorizeAttribute
{
    public PermissionAttribute(string permission)
    {
        Policy = $"Permission:{permission}";
    }
}
