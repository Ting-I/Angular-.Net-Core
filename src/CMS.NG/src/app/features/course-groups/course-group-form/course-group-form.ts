import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup, CourseGroupRequest } from '@core/models/course-group.model';

@Component({
  selector: 'app-course-group-form',
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, ToastModule],
  providers: [MessageService],
  templateUrl: './course-group-form.html',
  styleUrl: './course-group-form.scss',
})
export class CourseGroupForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  /** Shown as static text in edit mode — pkid is IDENTITY and never an editable control. */
  protected readonly pkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    description: ['', [Validators.required, Validators.maxLength(100)]],
  });

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    this.isEdit.set(rawId !== null);

    // No lookups to fetch, so there is nothing to forkJoin with the record load.
    if (rawId === null) {
      this.loading.set(false);
      return;
    }

    this.pkid.set(Number(rawId));
    this.service
      .getById(Number(rawId))
      .pipe(catchError(() => of(null)))
      .subscribe((courseGroup) => {
        if (courseGroup) {
          this.patchFromCourseGroup(courseGroup);
        } else {
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '查無此課程群組。',
          });
        }

        this.loading.set(false);
      });
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected cancel(): void {
    void this.router.navigate(['/course-groups']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request: CourseGroupRequest = {
      // pkid is IDENTITY: 0 on create, and the key from the route on edit.
      pkid: this.pkid() ?? 0,
      description: value.description.trim(),
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (courseGroup) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: courseGroup.description,
        });
        void this.router.navigate(['/course-groups', courseGroup.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: error.status === 404 ? '查無此課程群組。' : '請稍後再試。',
        });
      },
    });
  }

  private patchFromCourseGroup(courseGroup: CourseGroup): void {
    this.pkid.set(courseGroup.pkid);
    this.form.patchValue({ description: courseGroup.description });
  }
}
