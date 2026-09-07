import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus, PublishStatusRequest } from '@core/models/publish-status.model';

@Component({
  selector: 'app-publish-status-form',
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    ToggleSwitchModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './publish-status-form.html',
  styleUrl: './publish-status-form.scss',
})
export class PublishStatusForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  private pkid: number | null = null;

  protected readonly form = this.fb.nonNullable.group({
    pkid: [0, [Validators.required, Validators.min(0), Validators.max(255)]],
    description: ['', [Validators.required, Validators.maxLength(50)]],
    isDraft: [false],
    isPublished: [false],
    isDiscontinued: [false],
  });

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    this.pkid = rawId === null ? null : Number(rawId);
    this.isEdit.set(this.pkid !== null);

    // No lookups to fetch, so there is nothing to forkJoin with the record load.
    if (this.pkid === null) {
      this.loading.set(false);
      return;
    }

    this.service
      .getById(this.pkid)
      .pipe(catchError(() => of(null)))
      .subscribe((status) => {
        if (status) {
          this.patchFromStatus(status);
        } else {
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '查無此發布狀態。',
          });
        }

        // pkid is the primary key — immutable once created.
        this.form.controls.pkid.disable();
        this.loading.set(false);
      });
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected cancel(): void {
    void this.router.navigate(['/publish-statuses']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // getRawValue() includes the disabled pkid control in edit mode.
    const value = this.form.getRawValue();
    const request: PublishStatusRequest = {
      pkid: value.pkid,
      description: value.description.trim(),
      isDraft: value.isDraft,
      isPublished: value.isPublished,
      isDiscontinued: value.isDiscontinued,
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (status) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: status.description,
        });
        void this.router.navigate(['/publish-statuses', status.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: error.status === 409 ? '主代碼已存在。' : '請稍後再試。',
        });
      },
    });
  }

  private patchFromStatus(status: PublishStatus): void {
    this.form.patchValue({
      pkid: status.pkid,
      description: status.description,
      isDraft: status.isDraft,
      isPublished: status.isPublished,
      isDiscontinued: status.isDiscontinued,
    });
  }
}
