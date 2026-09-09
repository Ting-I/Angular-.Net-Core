import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { PartnerService } from '@core/services/partner.service';
import { Partner, PartnerRequest } from '@core/models/partner.model';

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-partner-form',
  imports: [
    RowAuditBadge,
    ReactiveFormsModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    ToastModule,
  ],
  providers: [MessageService],
  templateUrl: './partner-form.html',
  styleUrl: './partner-form.scss',
})
export class PartnerForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);

  protected readonly isEdit = signal(false);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  /** Shown as static text in edit mode — pkid is IDENTITY and never an editable control. */
  protected readonly pkid = signal<number | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(50)]],
    appKey: ['', [Validators.required, Validators.maxLength(10)]],
    nameOnPartnerMenu: ['', [Validators.required, Validators.maxLength(200)]],
    nameOnCourseDetailPage: ['', [Validators.required, Validators.maxLength(50)]],
    displayOrder: [0, [Validators.required]],
    imageFilename: ['', [Validators.maxLength(50)]],
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
      .subscribe((partner) => {
        if (partner) {
          this.patchFromPartner(partner);
        } else {
          this.messageService.add({
            severity: 'error',
            summary: '載入失敗',
            detail: '查無此原廠。',
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
    void this.router.navigate(['/partners']);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const imageFilename = value.imageFilename.trim();
    const request: PartnerRequest = {
      // pkid is IDENTITY: 0 on create, and the key from the route on edit.
      pkid: this.pkid() ?? 0,
      name: value.name.trim(),
      appKey: value.appKey.trim(),
      nameOnPartnerMenu: value.nameOnPartnerMenu.trim(),
      nameOnCourseDetailPage: value.nameOnCourseDetailPage.trim(),
      displayOrder: value.displayOrder,
      // Blank normalises to null so the hasImage filter's two arms stay honest.
      imageFilename: imageFilename === '' ? null : imageFilename,
    };

    this.saving.set(true);
    const save$ = this.isEdit() ? this.service.update(request) : this.service.create(request);

    save$.subscribe({
      next: (partner) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: '已儲存',
          detail: partner.name,
        });
        void this.router.navigate(['/partners', partner.pkid]);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'error',
          summary: '儲存失敗',
          detail: error.status === 404 ? '查無此原廠。' : '請稍後再試。',
        });
      },
    });
  }

  private patchFromPartner(partner: Partner): void {
    this.pkid.set(partner.pkid);
    this.form.patchValue({
      name: partner.name,
      appKey: partner.appKey,
      nameOnPartnerMenu: partner.nameOnPartnerMenu,
      nameOnCourseDetailPage: partner.nameOnCourseDetailPage,
      displayOrder: partner.displayOrder,
      imageFilename: partner.imageFilename ?? '',
    });
  }
}
