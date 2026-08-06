using CsvHelper.Configuration;

namespace DQEHelper.Models
{
    public class DqeCsvRecord
    {
        // Кастомные колонки (будут пустыми по умолчанию)
        public string Flag1_0 { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        public string CustomProvider { get; set; } = string.Empty;

        // Данные из исходного файла
        public string Screengrab { get; set; }
        public string Url { get; set; }
        public string DealsProvider { get; set; }
        public string HotelName { get; set; }
        public string DealsRatePlan { get; set; }
        public string DealsRoomType { get; set; }
        public string DealsRank { get; set; }
        public string DealsBreakfasts { get; set; }
        public string DealsCancellationPolicy { get; set; }
        public string DealsDiscount { get; set; }
        public string DealsExtras { get; set; }
        public string DealsExclusive { get; set; }
        public string DealsAllInclusive { get; set; }
        public string DealsInclusive { get; set; }
        public string DealsLandingPrice { get; set; }
        public string DealsNumberOfGuests { get; set; }
        public string DealsPaymentOptions { get; set; }
        public string DealsPriceRank { get; set; }
        public string DealsPromotions { get; set; }
        public string DealsTax { get; set; }
        public string DisplayPriceType { get; set; }
        public string Error { get; set; }
        public string Extras { get; set; }
        public string Hash { get; set; }
        public string IsFullStay { get; set; }
        public string RequestId { get; set; }
        public string ShopTime { get; set; }
        public string APIRequest { get; set; }
        public string ApiKey { get; set; }
        public string Deals { get; set; }
        public string DealsBrokerName { get; set; }
        public string DealsBenefits { get; set; }
    }

    public sealed class DqeCsvRecordMap : ClassMap<DqeCsvRecord>
    {
        public DqeCsvRecordMap()
        {
            // Index(N) жестко задает порядок колонок в выходном файле!
            Map(m => m.Flag1_0).Name("1/0").Index(0).Optional();
            Map(m => m.Comments).Name("comments").Index(1).Optional();
            Map(m => m.CustomProvider).Name("_provider").Index(2).Optional();
            
            Map(m => m.Screengrab).Name("screengrab").Index(3).Optional();
            Map(m => m.Url).Name("url").Index(4).Optional();
            Map(m => m.DealsProvider).Name("deals.provider").Index(5).Optional();
            Map(m => m.HotelName).Name("hotelName").Index(6).Optional();
            Map(m => m.DealsRatePlan).Name("deals.ratePlan").Index(7).Optional();
            Map(m => m.DealsRoomType).Name("deals.roomType").Index(8).Optional();
            Map(m => m.DealsRank).Name("deals.rank").Index(9).Optional();
            Map(m => m.DealsBreakfasts).Name("deals.breakfasts").Index(10).Optional();
            Map(m => m.DealsCancellationPolicy).Name("deals.cancellationPolicy").Index(11).Optional();
            Map(m => m.DealsDiscount).Name("deals.discount").Index(12).Optional();
            Map(m => m.DealsExtras).Name("deals.extras").Index(13).Optional();
            Map(m => m.DealsExclusive).Name("deals.exclusive").Index(14).Optional();
            Map(m => m.DealsAllInclusive).Name("deals.allInclusive").Index(15).Optional();
            Map(m => m.DealsInclusive).Name("deals.inclusive").Index(16).Optional();
            Map(m => m.DealsLandingPrice).Name("deals.landingPrice").Index(17).Optional();
            Map(m => m.DealsNumberOfGuests).Name("deals.numberOfGuests").Index(18).Optional();
            Map(m => m.DealsPaymentOptions).Name("deals.paymentOptions").Index(19).Optional();
            Map(m => m.DealsPriceRank).Name("deals.priceRank").Index(20).Optional();
            Map(m => m.DealsPromotions).Name("deals.promotions").Index(21).Optional();
            Map(m => m.DealsTax).Name("deals.tax").Index(22).Optional();
            Map(m => m.DisplayPriceType).Name("displayPriceType").Index(23).Optional();
            Map(m => m.Error).Name("error").Index(24).Optional();
            Map(m => m.Extras).Name("extras").Index(25).Optional();
            Map(m => m.Hash).Name("hash").Index(26).Optional();
            Map(m => m.IsFullStay).Name("isFullStay").Index(27).Optional();
            Map(m => m.RequestId).Name("requestId").Index(28).Optional();
            Map(m => m.ShopTime).Name("shopTime").Index(29).Optional();
            Map(m => m.APIRequest).Name("APIRequest").Index(30).Optional();
            Map(m => m.ApiKey).Name("apiKey").Index(31).Optional();
            Map(m => m.Deals).Name("deals").Index(32).Optional();
            Map(m => m.DealsBrokerName).Name("deals.BrokerName").Index(33).Optional();
            Map(m => m.DealsBenefits).Name("deals.benefits").Index(34).Optional();
        }
    }
}