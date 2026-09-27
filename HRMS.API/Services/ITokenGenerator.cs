using HRMS.API.Models;

namespace HRMS.API.Services;

public interface ITokenGenerator
{
    string Generate(User user);
}
