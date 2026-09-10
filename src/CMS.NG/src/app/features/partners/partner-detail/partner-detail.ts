import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { PartnerService } from '@core/services/partner.service';
import { Partner } from '@core/models/partner.model';

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-partner-detail',
  imports: [RowAuditBadge, ButtonModule],
  templateUrl: './partner-detail.html',
})
export class PartnerDetail implements OnInit {
  private readonly service = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly partner = signal<Partner | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    const rawId = this.route.snapshot.paramMap.get('id');
    const pkid = Number(rawId);
    if (!rawId || !Number.isInteger(pkid)) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // No FK lookups to load in parallel — a single record fetch is the whole page.
    this.service
      .getById(pkid)
      .pipe(catchError(() => of(null)))
      .subscribe((partner) => {
        this.partner.set(partner);
        this.notFound.set(partner === null);
        this.loading.set(false);
      });
  }

  /** Total across the five referencing tables — drives the "still in use" note. */
  protected referenceCount(partner: Partner): number {
    return (
      partner.certificationCount +
      partner.courseCount +
      partner.courseGroupCount +
      partner.promotionCount +
      partner.seminarCount
    );
  }

  protected back(): void {
    void this.router.navigate(['/partners']);
  }

  protected edit(): void {
    const pkid = this.partner()?.pkid;
    if (pkid !== undefined) {
      void this.router.navigate(['/partners', pkid, 'edit']);
    }
  }
}
