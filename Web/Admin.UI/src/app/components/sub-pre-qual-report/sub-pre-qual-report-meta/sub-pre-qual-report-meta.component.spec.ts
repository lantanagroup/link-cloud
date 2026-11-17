import { ComponentFixture, TestBed } from '@angular/core/testing';
import {ActivatedRoute, convertToParamMap} from '@angular/router';
import {provideHttpClient} from '@angular/common/http';
import {provideHttpClientTesting} from '@angular/common/http/testing';

import { SubPreQualReportMetaComponent } from './sub-pre-qual-report-meta.component';

describe('SubPreQualReportMetaComponent', () => {
  let component: SubPreQualReportMetaComponent;
  let fixture: ComponentFixture<SubPreQualReportMetaComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubPreQualReportMetaComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(),
        {provide: ActivatedRoute, useValue: {snapshot: {paramMap: convertToParamMap({})}}}]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SubPreQualReportMetaComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  [null, false, true].forEach(acceptable => {
    it(`displays acceptance metadata correctly when it is ${acceptable}`, () => {
      fixture.componentRef.setInput('category', {id: 'c', title: 'Category', severity: '', guidance: '', acceptable});
      fixture.detectChanges();
      const heading = Array.from(fixture.nativeElement.querySelectorAll('h6'))
        .find((element: any) => element.textContent.trim() === 'Acceptable') as HTMLElement;
      expect(heading.nextElementSibling?.textContent?.trim()).toBe(acceptable === null ? 'Unavailable' : String(acceptable));
    });
  });
});
