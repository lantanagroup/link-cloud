import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LinkAdminNavBarComponent } from './link-admin-nav-bar.component';

describe('LinkAdminNavBarComponent', () => {
  let component: LinkAdminNavBarComponent;
  let fixture: ComponentFixture<LinkAdminNavBarComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LinkAdminNavBarComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LinkAdminNavBarComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
