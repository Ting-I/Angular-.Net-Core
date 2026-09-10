import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import {
  AutoCompleteCompleteEvent,
  AutoCompleteModule,
  AutoCompleteSelectEvent,
} from 'primeng/autocomplete';
import { MessageService } from 'primeng/api';
import { Observable, of, throwError } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';

import { FeaturedPromoItemService } from '@core/services/featured-promo-item.service';
import { LookupService } from '@core/services/lookup.service';
import {
  FeaturedPromoItem,
  FeaturedPromoItemRequest,
} from '@core/models/featured-promo-item.model';
import { PromotionLookup } from '@core/models/promotion-lookup.model';

/**
 * Where a new item goes, plus optional text to start from. The list fills the first three from
 * the cell the operator clicked; Paste adds the rest from the copied row.
 */
export interface FeaturedPromoItemDraft {
  scheduleOn: string;
  trainingCenterPkid: number;
  slot: number;
  promoCode?: string;
  promotionPkid?: number;
  topic?: string;
  description?: string;
}

/**
 * Inline editor rendered inside a week-grid cell. Edit mode when `item` is set; otherwise a new
 * item is created at the `draft` position. The operator types a PromoCode, which is resolved to
 * Promotion_pkid before the save — either on selection, on 查詢, or as the first step of 儲存.
 */
import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-featured-promo-item-form',
  imports: [RowAuditBadge, ReactiveFormsModule, ButtonModule, InputTextModule, AutoCompleteModule],
  templateUrl: './featured-promo-item-form.html',
  styleUrl: './featured-promo-item-form.scss',
})
export class FeaturedPromoItemForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(FeaturedPromoItemService);
  private readonly lookupService = inject(LookupService);
  private readonly messageService = inject(MessageService);

  readonly item = input<FeaturedPromoItem | null>(null);
  readonly draft = input.required<FeaturedPromoItemDraft>();

  readonly saved = output<FeaturedPromoItem>();
  readonly cancelled = output<void>();

  protected readonly isEdit = computed(() => this.item() !== null);
  protected readonly saving = signal(false);
  protected readonly lookingUp = signal(false);

  /** PromoCodes offered by the autocomplete; `matches` keeps the rows behind them. */
  protected readonly suggestions = signal<string[]>([]);
  private matches: PromotionLookup[] = [];

  /** The promotion the current PromoCode has been resolved to, if any. */
  protected readonly promotion = signal<PromotionLookup | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    promoCode: ['', [Validators.required, Validators.maxLength(30)]],
    topic: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', [Validators.required, Validators.maxLength(300)]],
  });

  ngOnInit(): void {
    const item = this.item();
    if (item) {
      this.form.patchValue({
        promoCode: item.promotion.promoCode,
        topic: item.topic,
        description: item.description,
      });
      this.promotion.set(item.promotion);
      return;
    }

    const draft = this.draft();
    this.form.patchValue({
      promoCode: draft.promoCode ?? '',
      topic: draft.topic ?? '',
      description: draft.description ?? '',
    });

    // A pasted row already knows its promotion, so no lookup is needed before saving.
    if (draft.promoCode && draft.promotionPkid) {
      this.promotion.set({
        pkid: draft.promotionPkid,
        promoCode: draft.promoCode,
        topic: draft.topic ?? '',
        description: draft.description ?? '',
      });
    }
  }

  protected isInvalid(controlName: keyof typeof this.form.controls): boolean {
    const control = this.form.controls[controlName];
    return control.invalid && (control.dirty || control.touched);
  }

  protected searchPromoCodes(event: AutoCompleteCompleteEvent): void {
    this.lookupService.searchPromotions(event.query ?? '').subscribe({
      next: (promotions) => {
        this.matches = promotions;
        this.suggestions.set(promotions.map((p) => p.promoCode));
      },
      error: () => {
        this.matches = [];
        this.suggestions.set([]);
      },
    });
  }

  protected onPromoCodeSelected(event: AutoCompleteSelectEvent): void {
    const match = this.matches.find((p) => p.promoCode === event.value);
    if (match) {
      this.applyPromotion(match);
    }
  }

  /** The 查詢 button: resolve what was typed and pre-fill the empty text fields from it. */
  protected lookupPromoCode(): void {
    const control = this.form.controls.promoCode;
    control.markAsTouched();
    if (!control.value.trim()) {
      return;
    }

    this.lookingUp.set(true);
    this.resolvePromotion().subscribe({
      next: (promotion) => {
        this.lookingUp.set(false);
        if (promotion) {
          this.applyPromotion(promotion);
        } else {
          this.flagUnknownPromoCode();
        }
      },
      error: () => {
        this.lookingUp.set(false);
        this.messageService.add({ severity: 'error', summary: '查詢失敗', detail: '請稍後再試。' });
      },
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.resolvePromotion()
      .pipe(
        switchMap((promotion) => {
          if (!promotion) {
            return of(null);
          }
          const request = this.buildRequest(promotion);
          return this.isEdit() ? this.service.update(request) : this.service.create(request);
        }),
      )
      .subscribe({
        next: (item) => {
          this.saving.set(false);
          if (!item) {
            this.flagUnknownPromoCode();
            return;
          }
          this.saved.emit(item);
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'error',
            summary: '儲存失敗',
            detail:
              error.status === 409
                ? '此版位已有資料，請重新整理後再試。'
                : error.status === 404
                  ? '查無此上稿資料。'
                  : '請稍後再試。',
          });
        },
      });
  }

  /**
   * The typed code becomes a Promotion_pkid here. A code already resolved (edit mode, a pasted
   * row, or a prior selection) is reused; anything else goes to the exact-match lookup, where a
   * 404 means "no such code" rather than a failure.
   */
  private resolvePromotion(): Observable<PromotionLookup | null> {
    const code = this.form.controls.promoCode.value.trim();
    const known = this.promotion();
    if (known && known.promoCode === code) {
      return of(known);
    }

    return this.lookupService
      .getPromotionByCode(code)
      .pipe(
        catchError((error: HttpErrorResponse) =>
          error.status === 404 ? of(null) : throwError(() => error),
        ),
      );
  }

  /** Only empty text fields are pre-filled — existing wording on an edit is never overwritten. */
  private applyPromotion(promotion: PromotionLookup): void {
    this.promotion.set(promotion);
    const { topic, description } = this.form.getRawValue();
    this.form.patchValue({
      promoCode: promotion.promoCode,
      topic: topic.trim() ? topic : promotion.topic,
      description: description.trim() ? description : promotion.description,
    });
  }

  private flagUnknownPromoCode(): void {
    this.promotion.set(null);
    const control = this.form.controls.promoCode;
    control.setErrors({ unknownPromoCode: true });
    control.markAsTouched();
    this.messageService.add({
      severity: 'warn',
      summary: '查無此活動代碼',
      detail: control.value.trim(),
    });
  }

  private buildRequest(promotion: PromotionLookup): FeaturedPromoItemRequest {
    const value = this.form.getRawValue();
    const item = this.item();
    const draft = this.draft();
    return {
      // pkid is IDENTITY: 0 on create, and the existing key on edit.
      pkid: item?.pkid ?? 0,
      scheduleOn: item?.scheduleOn ?? draft.scheduleOn,
      trainingCenterPkid: item?.trainingCenterPkid ?? draft.trainingCenterPkid,
      slot: item?.slot ?? draft.slot,
      promotionPkid: promotion.pkid,
      topic: value.topic.trim(),
      description: value.description.trim(),
    };
  }
}
