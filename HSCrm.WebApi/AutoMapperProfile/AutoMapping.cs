using AutoMapper;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;

namespace Automation.WebApi.AutoMapperProfile
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<ApplicationUser, UserEditModel>().ReverseMap();
        }
    }
}
