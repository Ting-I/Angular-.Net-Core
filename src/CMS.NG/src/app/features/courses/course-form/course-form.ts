import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { DatePickerModule } from 'primeng/datepicker';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseService } from '@core/services/course.service';
import { LookupService } from '@core/services/lookup.service';
import { Course, CourseRequest } from '@core/models/course.model';
import { PartnerLookup } from '@core/models/partner-lookup.model';
import { CourseGroupLookup } from '@core/models/course-group-lookup.model';
import { PublishStatusLookup } from '@core/models/publish-status-lookup.model';
import { CertificationLookup } from '@core/models/certification-lookup.model';
import { JobCategoryLookup } from '@core/models/job-category-lookup.model';
import { addYears, fromIso, toIso } from '@core/utils/date.util';

/** Years added to ScheduleOn when defaulting ScheduleOff on a fresh pick. */
const SCHEDULE_OFF_YEARS = 10;

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-form',
  imports: [
    RowAuditBadge,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    TextareaModule,
    SelectModule,
    MultiSelectModule,
    DatePickerModule,
    ToggleSwitchModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './course-form.html',
  styleUrl: './course-form.scss',
})
export class CourseForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseService);
  private readonly lookupService = inject(LookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  /** Shown as static text in edit mode — pkid is IDENTITY and never an editable control. */
  protected readonly pkid = signal<number | null>(null);

  protected readonly partners = signal<PartnerLookup[]>([]);
  protected readonly courseGroups = signal<CourseGroupLookup[]>([]);
  protected readonly publishStatuses = signal<PublishStatusLookup[]>([]);
  protected readonly certifications = signal<CertificationLookup[]>([]);
  protected readonly jobCategories = signal<JobCategoryLookup[]>([]);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    officialTitle: ['', [Validators.maxLength(300)]],
    courseId: ['', [Validators.required, Validators.maxLength(50)]],
    prodCourseId: ['', [Validators.required, Validators.maxLength(50)]],
    friendlyUrl: ['', [Validators.required, Validators.maxLength(100)]],
    displayOrder: [0, [Validators.required]],
    partnerPkid: [null as number | null, [Validators.required]],
    courseGroupPkid: [null as number | null],
    publishStatusPkid: [null as number | null, [Validators.required]],
    scheduleOn: [null as Date | null, [Validators.required]],
    scheduleOff: [null as Date | null, [Validators.required]],
    hour: [0, [Validators.required, Validators.min(0)]],
    listPrice: [0, [Validators.required, Validators.min(0)]],
    learningCredit: [0, [Validators.required, Validators.min(0)]],
    material: ['', [Validators.maxLength(500)]],
    objective: ['', [Validators.maxLength(4000)]],
    target: ['', [Validators.maxLength(500)]],
    prerequisites: ['', [Validators.maxLength(4000)]],
    outline: [''],
    towardCertOrExam: [''],
    note: ['', [Validators.maxLength(4000)]],
    otherInfo: ['', [Validators.maxLength(4000)]],
    canRepeat: [false],
    certificationPkids: [[] as number[]],
    jobCategoryPkids: [[] as number[]],
  });

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(rawId !== null);

    // Picking 上架日期 defaults 下架日期 ten years on. emitEvent:false keeps it from looping, and
    // in edit mode the record patch writes scheduleOff after scheduleOn, so the stored value wins.
    this.form.controls.scheduleOn.valueChanges.subscribe((value) => {
      if (value instanceof Date && !Number.isNaN(value.getTime())) {
        this.form.controls.scheduleOff.setValue(addYears(value, SCHEDULE_OFF_YEARS), {
          emitEvent: false,
        });
      }
    });

    const lookups = {
      partners: this.lookupService.getPartners().pipe(catchError(() => of([]))),
      courseGroups: this.lookupService.getCourseGroups().pipe(catchError(() => of([]))),
      publishStatuses: this.lookupService.getPublishStatuses().pipe(catchError(() => of([]))),
      certifications: this.lookupService.getCertifications().pipe(catchError(() => of([]))),
      jobCategories: this.lookupService.getJobCategories().pipe(catchError(() => of([]))),
    };

    forkJoin({
      ...lookups,
      course:
        rawId === null
          ? of(null)
          : this.service.getById(Number(rawId)).pipe(catchError(() => of(null))),
    }).subscribe(({ partners, courseGroups, publishStatuses, certifications, jobCategories, course }) => {
      this.partners.set(partners);
      this.courseGroups.set(courseGroups);
      this.publishStatuses.set(publishStatuses);
      this.certifications.set(certifications);
      this.jobCategories.set(jobCategories);

      if (rawId !== null) {
        if (course) {
          this.patchFromCourse(course);
        } else {
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '查無此課程。',
          });
        }
      }

      this.loading.set(false);
    });
  }

  protected get partnerOptions(): { pkid: number; label: string }[] {
    return this.partners().map((p) => ({ pkid: p.pkid, label: `${p.name}（${p.appKey}）` }));
  }

  protected get courseGroupOptions(): { pkid: number; label: string }[] {
    return this.courseGroups().map((g) => ({ pkid: g.pkid, label: g.description }));
  }

  protected get publishStatusOptions(): { pkid: number; label: string }[] {
    return this.publishStatuses().map((s) => ({ pkid: s.pkid, label: s.description }));
  }

  /** Certification.Title is nullable in the schema, so a blank one falls back to its pkid. */
  protected get certificationOptions(): { pkid: number; label: string }[] {
    return this.certifications().map((c) => ({
      pkid: c.pkid,
      label: `${c.partnerName} — ${c.title || `認證 #${c.pkid}`}`,
    }));
  }

  protected get jobCategoryOptions(): { pkid: number; label: string }[] {
    return this.jobCategories().map((j) => ({ pkid: j.pkid, label: j.description }));
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected cancel(): void {
    void this.router.navigate(['/courses']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: CourseRequest = {
      // pkid is IDENTITY: 0 on create, and the key from the route on edit.
      pkid: this.pkid() ?? 0,
      title: value.title.trim(),
      officialTitle: this.orNull(value.officialTitle),
      courseId: value.courseId.trim(),
      prodCourseId: value.prodCourseId.trim(),
      friendlyUrl: value.friendlyUrl.trim(),
      displayOrder: value.displayOrder,
      partnerPkid: value.partnerPkid as number,
      courseGroupPkid: value.courseGroupPkid,
      publishStatusPkid: value.publishStatusPkid as number,
      // Local components, never toISOString() — that would shift a UTC+8 date back a day.
      scheduleOn: toIso(value.scheduleOn),
      scheduleOff: toIso(value.scheduleOff),
      hour: value.hour,
      listPrice: value.listPrice,
      learningCredit: value.learningCredit,
      material: this.orNull(value.material),
      objective: this.orNull(value.objective),
      target: this.orNull(value.target),
      prerequisites: this.orNull(value.prerequisites),
      outline: this.orNull(value.outline),
      towardCertOrExam: this.orNull(value.towardCertOrExam),
      note: this.orNull(value.note),
      otherInfo: this.orNull(value.otherInfo),
      canRepeat: value.canRepeat,
      certificationPkids: value.certificationPkids,
      jobCategoryPkids: value.jobCategoryPkids,
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (course) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: course.title,
        });
        void this.router.navigate(['/courses', course.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: error.status === 404 ? '查無此課程。' : '請稍後再試。',
        });
      },
    });
  }

  /** Blank normalises to null so the ten nullable columns do not collect a mix of '' and NULL. */
  private orNull(value: string): string | null {
    const trimmed = value.trim();
    return trimmed === '' ? null : trimmed;
  }

  private patchFromCourse(course: Course): void {
    this.pkid.set(course.pkid);
    this.form.patchValue({
      title: course.title,
      officialTitle: course.officialTitle ?? '',
      courseId: course.courseId,
      prodCourseId: course.prodCourseId,
      friendlyUrl: course.friendlyUrl,
      displayOrder: course.displayOrder,
      partnerPkid: course.partnerPkid,
      courseGroupPkid: course.courseGroupPkid,
      publishStatusPkid: course.publishStatusPkid,
      // scheduleOn first, then scheduleOff: the scheduleOn patch fires the auto-default
      // subscription, and this patch overwrites it with the stored value.
      scheduleOn: fromIso(course.scheduleOn),
      scheduleOff: fromIso(course.scheduleOff),
      hour: course.hour,
      listPrice: course.listPrice,
      learningCredit: course.learningCredit,
      material: course.material ?? '',
      objective: course.objective ?? '',
      target: course.target ?? '',
      prerequisites: course.prerequisites ?? '',
      outline: course.outline ?? '',
      towardCertOrExam: course.towardCertOrExam ?? '',
      note: course.note ?? '',
      otherInfo: course.otherInfo ?? '',
      canRepeat: course.canRepeat,
      certificationPkids: course.certificationPkids,
      jobCategoryPkids: course.jobCategoryPkids,
    });
  }
}
