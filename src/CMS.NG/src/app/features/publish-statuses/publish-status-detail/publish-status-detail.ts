import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { PublishStatusService } from '@core/services/publish-status.service';
import { PublishStatus } from '@core/models/publish-status.model';

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-publish-status-detail',
  imports: [RowAuditBadge, ButtonModule, TagModule],
  templateUrl: './publish-status-detail.html',
  styleUrl: './publish-status-detail.scss',
})
export class PublishStatusDetail implements OnInit {
  private readonly service = inject(PublishStatusService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly status = signal<PublishStatus | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);

  ngOnInit(): void {
    const pkid = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(pkid) || !this.route.snapshot.paramMap.get('id')) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }

    // No FK lookups to load in parallel — a single record fetch is the whole page.
    this.service
      .getById(pkid)
      .pipe(catchError(() => of(null)))
      .subscribe((status) => {
        this.status.set(status);
        this.notFound.set(status === null);
        this.loading.set(false);
      });
  }

  protected back(): void {
    void this.router.navigate(['/publish-statuses']);
  }

  protected edit(): void {
    const pkid = this.status()?.pkid;
    if (pkid !== undefined) {
      void this.router.navigate(['/publish-statuses', pkid, 'edit']);
    }
  }
}
