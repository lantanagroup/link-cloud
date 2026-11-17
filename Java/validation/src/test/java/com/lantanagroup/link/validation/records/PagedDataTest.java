package com.lantanagroup.link.validation.records;

import org.junit.jupiter.api.Test;
import org.springframework.data.domain.PageImpl;
import org.springframework.data.domain.PageRequest;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;

class PagedDataTest {

    @Test
    void fromMapsContentAndPagingMetadata() {
        PageImpl<Integer> page = new PageImpl<>(List.of(1, 2, 3), PageRequest.of(2, 3), 20);

        PagedData<String> result = PagedData.from(page, i -> "item-" + i);

        assertThat(result.content()).containsExactly("item-1", "item-2", "item-3");
        assertThat(result.page()).isEqualTo(2);
        assertThat(result.size()).isEqualTo(3);
        assertThat(result.totalElements()).isEqualTo(20);
        assertThat(result.totalPages()).isEqualTo(page.getTotalPages());
    }

    @Test
    void fromAnEmptyPageProducesEmptyContent() {
        PageImpl<Integer> page = new PageImpl<>(List.of(), PageRequest.of(0, 10), 0);

        PagedData<String> result = PagedData.from(page, i -> "item-" + i);

        assertThat(result.content()).isEmpty();
        assertThat(result.totalElements()).isZero();
    }
}
