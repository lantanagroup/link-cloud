package com.lantanagroup.link.measureeval.repositories;

import com.lantanagroup.link.measureeval.entities.Resource;

public interface ResourceUpsertingRepository {
    Resource upsert(Resource entity);
}
