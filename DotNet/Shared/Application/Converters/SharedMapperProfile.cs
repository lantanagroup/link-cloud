using AutoMapper;
using Google.Protobuf.WellKnownTypes;

namespace LantanaGroup.Link.Shared.Application.Converters
{
    public class SharedMapperProfile : Profile
    {
        public SharedMapperProfile()
        {

            // protobuf types to .net types
            CreateMap<string, Guid>().ConvertUsing(s => string.IsNullOrEmpty(s) ? Guid.Empty : Guid.Parse(s));
            CreateMap<string, Guid?>().ConvertUsing(s => string.IsNullOrEmpty(s) ? null : Guid.Parse(s));
            CreateMap<Timestamp, DateTime>().ConvertUsing(s => s == null ? DateTime.MinValue : s.ToDateTime());
            CreateMap<Timestamp, DateTime?>().ConvertUsing(s => s == null ? null : s.ToDateTime());


            // .net types to protobuf types
            CreateMap<Guid, string>().ConvertUsing(s => s == Guid.Empty ? string.Empty : s.ToString());
            CreateMap<Guid?, string>().ConvertUsing(s => s.HasValue ? s.Value.ToString() : string.Empty);
            CreateMap<DateTime, Timestamp>().ConvertUsing(s => Timestamp.FromDateTime(s.ToUniversalTime()));
            CreateMap<DateTime?, Timestamp>().ConvertUsing(s => s.HasValue ? Timestamp.FromDateTime(s.Value.ToUniversalTime()) : null);
        }
    }
}
