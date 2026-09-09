import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

import { CourseGroupService } from '@core/services/course-group.service';
import { CourseGroup } from '@core/models/course-group.model';

import { RowAuditBadge } from '@core/components/row-audit-badge/row-audit-badge';

@Component({
  selector: 'app-course-group-detail',
  imports: [RowAuditBadge, ButtonModule],
  templateUrl: './course-group-detail.html',
  styleUrl: './course-group-detail.scss',
})
export class CourseGroupDetail implements OnInit {
  private readonly service = inject(CourseGroupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly courseGroup = signal<CourseGroup | null>(null);
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
      .subscribe((courseGroup) => {
        this.courseGroup.set(courseGroup);
        this.notFound.set(courseGroup === null);
        this.loading.set(false);
      });
  }

  /** Total across the two referencing tables — drives the "still in use" note. */
  protected referenceCount(courseGroup: CourseGroup): number {
    return courseGroup.courseCount + courseGroup.partnerCourseGroupCount;
  }

  protected back(): void {
    void this.router.navigate(['/course-groups']);
  }

  protected edit(): void {
    const pkid = this.courseGroup()?.pkid;
    if (pkid !== undefined) {
      void this.router.navigate(['/course-groups', pkid, 'edit']);
    }
  }
}
