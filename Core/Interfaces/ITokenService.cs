using System.Collections.Generic;
using Core.Entities.Identity;

namespace Core.Interfaces
{
    public interface ITokenService
    {
        /// <summary>Creates the JWT. Roles (e.g. "Admin") are added as role claims when given.</summary>
        string CreateToken(AppUser user, IEnumerable<string> roles = null);
    }
}