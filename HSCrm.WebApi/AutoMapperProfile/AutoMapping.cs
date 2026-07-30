using AutoMapper;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;

namespace Automation.WebApi.AutoMapperProfile
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            CreateMap<PaymentCreateModel, Payment>().ReverseMap();
            CreateMap<PaymentEditModel, Payment>().ReverseMap();
            CreateMap<Payment, PaymentModel>().ReverseMap();

            CreateMap<ReceiptCreateModel, Receipt>().ReverseMap();
            CreateMap<ReceiptEditModel, Receipt>().ReverseMap();
            CreateMap<Receipt, ReceiptModel>().ReverseMap();

            CreateMap<ApplicationUser, UserEditModel>().ReverseMap();


            CreateMap<PurchaseInvoice, PurchaseInvoiceModel>().ReverseMap();
            CreateMap<PurchaseInvoiceItem, PurchaseInvoiceItemModel>().ReverseMap();

            CreateMap<InvoiceCreateModel, PurchaseInvoice>().ReverseMap();
        }
    }
}
