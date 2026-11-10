package com.lantanagroup.link.validation.repositories;

import jakarta.persistence.EntityManager;
import org.hibernate.engine.spi.SessionFactoryImplementor;
import org.hibernate.dialect.SQLServerDialect;

public class ResultPagingCountsImpl implements ResultPagingCounts {
    private final EntityManager entityManager;

    public ResultPagingCountsImpl(EntityManager entityManager) {
        this.entityManager = entityManager;
    }

    @Override
    public long countSavedCategoryResults(String facilityId, String reportId, String categoryId) {
        boolean uncategorized = "uncategorized".equalsIgnoreCase(categoryId);
        String sql = "SELECT COUNT(*) FROM result r "
                + (uncategorized ? "" : "JOIN result_category rc ON rc.result_id = r.id ")
                + "WHERE r.facility_id = :facilityId AND r.report_id = :reportId "
                + (uncategorized
                    ? "AND NOT EXISTS (SELECT 1 FROM result_category rc WHERE rc.result_id = r.id)"
                    : "AND rc.category_id = :categoryId");
        // Report sizes vary widely. Reusing a small report's nested-loop count plan can
        // cause millions of reads on a large report. Keep this hint specific to SQL Server.
        if (entityManager.getEntityManagerFactory().unwrap(SessionFactoryImplementor.class)
                .getJdbcServices().getDialect() instanceof SQLServerDialect) {
            sql = sql.replace("COUNT(*)", "COUNT_BIG(*)") + " OPTION (RECOMPILE)";
        }
        var query = entityManager.createNativeQuery(sql)
                .setParameter("facilityId", facilityId)
                .setParameter("reportId", reportId);
        if (!uncategorized) query.setParameter("categoryId", categoryId);
        return ((Number) query.getSingleResult()).longValue();
    }
}
