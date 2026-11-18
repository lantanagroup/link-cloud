package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Rubric;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.records.RubricSearch;
import jakarta.persistence.criteria.Predicate;
import jakarta.persistence.criteria.Root;
import jakarta.persistence.criteria.Subquery;
import org.springframework.data.jpa.domain.Specification;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * The rubric list's filters as a query. Built from criteria rather than written as one JPQL string with
 * optional parameters, because a null parameter in {@code :p is null or ...} reaches SQL Server without a
 * type, and each filter that is not asked for is simply left out here.
 */
public final class RubricSpecifications {

    private static final char ESCAPE = '\\';

    private RubricSpecifications() {
    }

    public static Specification<Rubric> matching(RubricSearch search) {
        return (root, query, builder) -> {
            List<Predicate> predicates = new ArrayList<>();

            if (search.status() != null) {
                Subquery<Long> versions = query.subquery(Long.class);
                Root<RubricVersion> version = versions.from(RubricVersion.class);
                versions.select(version.get("rubricVersionId"))
                        .where(builder.equal(version.get("rubricId"), root.get("rubricId")),
                                builder.equal(version.get("status"), search.status()));
                predicates.add(builder.exists(versions));
            }

            if (search.search() != null) {
                String pattern = "%" + escapeLike(search.search().toLowerCase(Locale.ROOT)) + "%";
                predicates.add(builder.or(
                        builder.like(builder.lower(root.get("rubricId")), pattern, ESCAPE),
                        builder.like(builder.lower(root.get("title")), pattern, ESCAPE),
                        builder.like(builder.lower(root.get("owner")), pattern, ESCAPE)));
            }

            if (search.owner() != null) {
                predicates.add(builder.equal(root.get("owner"), search.owner()));
            }

            return builder.and(predicates.toArray(Predicate[]::new));
        };
    }

    // what the user typed is text to find, not a pattern: % and _ match themselves
    static String escapeLike(String text) {
        return text
                .replace(String.valueOf(ESCAPE), "" + ESCAPE + ESCAPE)
                .replace("%", ESCAPE + "%")
                .replace("_", ESCAPE + "_");
    }
}
