using AutoMapper;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;

namespace Automation.WebApi.AutoMapperProfile
{
    public class AutoMapping : Profile
    {
        public AutoMapping()
        {
            // Payment Mappings
            CreateMap<PaymentCreateModel, Payment>().ReverseMap();
            CreateMap<PaymentEditModel, Payment>().ReverseMap();
            CreateMap<Payment, PaymentModel>().ReverseMap();

            // Receipt Mappings
            CreateMap<ReceiptCreateModel, Receipt>().ReverseMap();
            CreateMap<ReceiptEditModel, Receipt>().ReverseMap();
            CreateMap<Receipt, ReceiptModel>()
                .ForMember(dest => dest.BankAccountTitle, opt => opt.MapFrom(src => src.BankAccount != null ? $"{src.BankAccount.Title} ({src.BankAccount.BankName})" : null))
                .ReverseMap();

            // User Mappings
            CreateMap<ApplicationUser, UserEditModel>().ReverseMap();

            // ==========================================
            // ✅ Purchase Invoice Mappings (نقشه‌برداری فاکتور خرید)
            // ==========================================
            CreateMap<PurchaseInvoiceCreateModel, PurchaseInvoice>();
            CreateMap<PurchaseInvoiceItemCreateModel, PurchaseInvoiceItem>()
                .ForMember(dest => dest.UnitPricePurchase, opt => opt.MapFrom(src => src.UnitPrice));

            CreateMap<PurchaseInvoiceEditModel, PurchaseInvoice>();
            CreateMap<PurchaseInvoiceItemEditModel, PurchaseInvoiceItem>()
                .ForMember(dest => dest.UnitPricePurchase, opt => opt.MapFrom(src => src.UnitPrice));

            CreateMap<PurchaseInvoice, PurchaseInvoiceModel>().ReverseMap();
            // در صورتی که کلاس PurchaseInvoiceItemModel مجزا برای نمایش آیتم‌های خرید دارید:
            // CreateMap<PurchaseInvoiceItem, PurchaseInvoiceItemModel>().ReverseMap();

            // ==========================================
            // ✅ Sales Invoice Mappings (نقشه‌برداری فاکتور فروش)
            // ==========================================
            CreateMap<SalesInvoiceCreateModel, SalesInvoice>();
            CreateMap<SalesInvoiceItemCreateModel, SalesInvoiceItem>()
                .ForMember(dest => dest.UnitPriceSell, opt => opt.MapFrom(src => src.UnitPrice));

            CreateMap<SalesInvoiceEditModel, SalesInvoice>();
            CreateMap<SalesInvoiceItemEditModel, SalesInvoiceItem>();

            CreateMap<SalesInvoice, SalesInvoiceModel>().ReverseMap();
        }
    }
}