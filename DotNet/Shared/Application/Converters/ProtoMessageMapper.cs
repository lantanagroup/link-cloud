using AutoMapper;

namespace LantanaGroup.Link.Shared.Application.Converters
{
    public class ProtoMessageMapper<TSource, TDestination>
    {

        MapperConfiguration _cfg;

        public ProtoMessageMapper(Profile? profile = null)
        {
            _cfg = new MapperConfiguration(cfg =>
            {
                cfg.CreateMap<TSource, TDestination>();
                cfg.AddProfile(new SharedMapperProfile());

                if (profile != null)
                {
                    cfg.AddProfile(profile);
                }
            });

            _cfg.CompileMappings();
        }

        public IMapper CreateMapper()
        {
            return _cfg.CreateMapper();
        }

    }
}
