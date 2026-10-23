package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.PatientCategoryCount;
import com.lantanagroup.link.validation.entities.ReportCategoryCount;
import com.lantanagroup.link.validation.entities.ReportCategoryCountStatus;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.models.CategoryCountModel;
import com.lantanagroup.link.validation.models.ReportCategoryCountsModel;
import com.lantanagroup.link.validation.repositories.PatientCategoryCountRepository;
import com.lantanagroup.link.validation.repositories.ReportCategoryCountRepository;
import com.lantanagroup.link.validation.repositories.ReportCategoryCountStatusRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.PlatformTransactionManager;
import org.springframework.transaction.support.TransactionTemplate;

import java.time.OffsetDateTime;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.function.Function;

/**
 * Keeps each report's per-category validation counts, which outlive the report's findings.
 * See dev-docs/validation-category-counts.md.
 */
@Service
public class CategoryCountService {
    private static final Logger logger = LoggerFactory.getLogger(CategoryCountService.class);

    private final ReportCategoryCountRepository reportCategoryCountRepository;
    private final PatientCategoryCountRepository patientCategoryCountRepository;
    private final ReportCategoryCountStatusRepository reportCategoryCountStatusRepository;
    private final TransactionTemplate transactionTemplate;

    public CategoryCountService(
            ReportCategoryCountRepository reportCategoryCountRepository,
            PatientCategoryCountRepository patientCategoryCountRepository,
            ReportCategoryCountStatusRepository reportCategoryCountStatusRepository,
            PlatformTransactionManager transactionManager) {
        this.reportCategoryCountRepository = reportCategoryCountRepository;
        this.patientCategoryCountRepository = patientCategoryCountRepository;
        this.reportCategoryCountStatusRepository = reportCategoryCountStatusRepository;
        this.transactionTemplate = new TransactionTemplate(transactionManager);
    }

    /**
     * Replaces one patient's contribution to its report's category counts with the counts from {@code results}.
     * Repeating it for the same patient (a redelivered message, for example) replaces the earlier counts rather
     * than adding to them. A finding is counted once in each of its categories, whatever their submit flag, and as
     * uncategorized when it has no category at all. Each total records its category's submit flag so readers can
     * filter on it.
     */
    public void recordPatientCounts(String facilityId, String reportId, String patientId, List<Result> results) {
        Map<String, CategoryTally> tallies = countByCategory(results);
        ensureStatusExists(facilityId, reportId);
        transactionTemplate.executeWithoutResult(
                transaction -> applyPatientCounts(facilityId, reportId, patientId, tallies));
        logger.debug("Recorded validation counts in {} categories facility={} report={}",
                tallies.size(),
                LogUtils.sanitize(facilityId),
                LogUtils.sanitize(reportId));
    }

    /**
     * Gets a report's stored category counts, most issues first, optionally only the categories with the given
     * submit flag. Empty when the report's counts were never written, which is different from a report whose counts
     * exist but list no categories.
     */
    public Optional<ReportCategoryCountsModel> getReportCounts(String facilityId, String reportId, Boolean submit) {
        Optional<ReportCategoryCountStatus> status = reportCategoryCountStatusRepository
                .findByFacilityIdAndReportId(facilityId, reportId);
        if (status.isEmpty()) {
            return Optional.empty();
        }
        List<ReportCategoryCount> totals = submit == null
                ? reportCategoryCountRepository.findAllByFacilityIdAndReportId(facilityId, reportId)
                : reportCategoryCountRepository.findAllByFacilityIdAndReportIdAndSubmit(facilityId, reportId, submit);
        List<CategoryCountModel> categories = totals.stream()
                .sorted(Comparator.comparingLong(ReportCategoryCount::getIssues)
                        .reversed()
                        .thenComparing(ReportCategoryCount::getCategoryId))
                .map(total -> new CategoryCountModel(
                        total.getCategoryId(),
                        total.getTitle(),
                        total.isAcceptable(),
                        total.isSubmit(),
                        total.getIssues()))
                .toList();
        return Optional.of(new ReportCategoryCountsModel(
                facilityId,
                reportId,
                status.get().getLastUpdated(),
                categories));
    }

    private static Map<String, CategoryTally> countByCategory(List<Result> results) {
        // Category ids compare case-insensitively under the database collation, so group them the same way.
        Map<String, CategoryTally> tallies = new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
        for (Result result : results) {
            List<Category> categories = result.getCategories();
            if (categories == null || categories.isEmpty()) {
                tally(tallies, Category.UNCATEGORIZED);
                continue;
            }
            Set<String> counted = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
            for (Category category : categories) {
                if (category.getId() != null && counted.add(category.getId())) {
                    tally(tallies, category);
                }
            }
        }
        return tallies;
    }

    private static void tally(Map<String, CategoryTally> tallies, Category category) {
        String title = category.getTitle() != null ? category.getTitle() : category.getId();
        tallies.merge(category.getId(),
                new CategoryTally(category.getId(), title, category.isAcceptable(), category.isSubmit(), 1),
                (existing, added) -> existing.plusOne());
    }

    /**
     * Creates the report's status row in a transaction of its own. Two patients of a new report can race to create
     * it; the loser's insert fails on the unique constraint, and catching that inside the counting transaction would
     * leave it marked rollback-only.
     */
    private void ensureStatusExists(String facilityId, String reportId) {
        if (reportCategoryCountStatusRepository.findByFacilityIdAndReportId(facilityId, reportId).isPresent()) {
            return;
        }
        try {
            transactionTemplate.executeWithoutResult(transaction -> {
                ReportCategoryCountStatus status = new ReportCategoryCountStatus();
                status.setFacilityId(facilityId);
                status.setReportId(reportId);
                status.setLastUpdated(OffsetDateTime.now());
                reportCategoryCountStatusRepository.saveAndFlush(status);
            });
        } catch (DataIntegrityViolationException e) {
            logger.debug("Validation count status for report={} was created by another patient first",
                    LogUtils.sanitize(reportId));
        }
    }

    private void applyPatientCounts(String facilityId,
                                    String reportId,
                                    String patientId,
                                    Map<String, CategoryTally> tallies) {
        // Locking the status row first serializes count updates for one report; other reports run in parallel.
        ReportCategoryCountStatus status = reportCategoryCountStatusRepository
                .findWithLockByFacilityIdAndReportId(facilityId, reportId)
                .orElseThrow(() -> new IllegalStateException("No validation count status for the report"));
        Map<String, ReportCategoryCount> totals = byCategoryId(
                reportCategoryCountRepository.findAllByFacilityIdAndReportId(facilityId, reportId),
                ReportCategoryCount::getCategoryId);
        Map<String, PatientCategoryCount> previous = byCategoryId(
                patientCategoryCountRepository.findAllByFacilityIdAndReportIdAndPatientId(
                        facilityId,
                        reportId,
                        patientId),
                PatientCategoryCount::getCategoryId);

        Set<String> categoryIds = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
        categoryIds.addAll(previous.keySet());
        categoryIds.addAll(tallies.keySet());
        for (String categoryId : categoryIds) {
            PatientCategoryCount patientCount = previous.get(categoryId);
            CategoryTally tally = tallies.get(categoryId);
            long before = patientCount != null ? patientCount.getIssues() : 0;
            long after = tally != null ? tally.issues() : 0;
            replacePatientCount(facilityId, reportId, patientId, patientCount, tally);
            adjustReportTotal(facilityId, reportId, totals.get(categoryId), tally, after - before);
        }
        status.setLastUpdated(OffsetDateTime.now());
    }

    private void replacePatientCount(String facilityId,
                                     String reportId,
                                     String patientId,
                                     PatientCategoryCount patientCount,
                                     CategoryTally tally) {
        if (tally == null) {
            patientCategoryCountRepository.delete(patientCount);
            return;
        }
        if (patientCount != null) {
            patientCount.setIssues(tally.issues());
            return;
        }
        PatientCategoryCount created = new PatientCategoryCount();
        created.setFacilityId(facilityId);
        created.setReportId(reportId);
        created.setPatientId(patientId);
        created.setCategoryId(tally.categoryId());
        created.setIssues(tally.issues());
        patientCategoryCountRepository.save(created);
    }

    private void adjustReportTotal(String facilityId,
                                   String reportId,
                                   ReportCategoryCount total,
                                   CategoryTally tally,
                                   long delta) {
        if (total == null) {
            if (tally == null || delta <= 0) {
                return;
            }
            ReportCategoryCount created = new ReportCategoryCount();
            created.setFacilityId(facilityId);
            created.setReportId(reportId);
            created.setCategoryId(tally.categoryId());
            created.setTitle(tally.title());
            created.setAcceptable(tally.acceptable());
            created.setSubmit(tally.submit());
            created.setIssues(delta);
            reportCategoryCountRepository.save(created);
            return;
        }
        long issues = total.getIssues() + delta;
        if (issues <= 0) {
            reportCategoryCountRepository.delete(total);
            return;
        }
        total.setIssues(issues);
        if (tally != null) {
            total.setTitle(tally.title());
            total.setAcceptable(tally.acceptable());
            total.setSubmit(tally.submit());
        }
    }

    private static <T> Map<String, T> byCategoryId(List<T> rows, Function<T, String> categoryId) {
        Map<String, T> byId = new TreeMap<>(String.CASE_INSENSITIVE_ORDER);
        for (T row : rows) {
            byId.put(categoryId.apply(row), row);
        }
        return byId;
    }

    private record CategoryTally(String categoryId, String title, boolean acceptable, boolean submit, long issues) {
        CategoryTally plusOne() {
            return new CategoryTally(categoryId, title, acceptable, submit, issues + 1);
        }
    }
}
