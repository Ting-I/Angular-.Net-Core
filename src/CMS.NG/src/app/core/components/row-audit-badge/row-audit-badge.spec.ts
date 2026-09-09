import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';

import { environment } from '@env';
import { RowAuditEntry } from '@core/models/row-audit.model';
import { HISTORY_UNAVAILABLE_TEXT, NO_HISTORY_TEXT, RowAuditBadge } from './row-audit-badge';

describe('RowAuditBadge', () => {
  let fixture: ComponentFixture<RowAuditBadge>;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiUrl}/rowaudit`;

  /** Newest first, exactly as the API returns it. */
  const trail: RowAuditEntry[] = [
    {
      dateTime: '2026-06-04T14:30:00',
      userName: 'alice',
      actionType: 'Update',
      actionDesc: 'Title,DisplayOrder',
    },
    { dateTime: '2026-06-02T08:05:00', userName: 'bob', actionType: 'Update', actionDesc: 'Title' },
    { dateTime: '2026-06-01T09:00:00', userName: 'bob', actionType: 'Insert', actionDesc: 'C-001' },
  ];

  /** Renders the badge for one record. Signal inputs are set before the first change detection. */
  function render(tableName: string, pkid: number | null): void {
    fixture = TestBed.createComponent(RowAuditBadge);
    fixture.componentRef.setInput('tableName', tableName);
    fixture.componentRef.setInput('pkid', pkid);
    fixture.detectChanges();
  }

  /** The one outstanding history request. */
  function expectRequest() {
    return httpMock.expectOne((req) => req.url === baseUrl);
  }

  function badgeText(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function latestText(): string {
    const latest = (fixture.nativeElement as HTMLElement).querySelector('.row-audit-latest');
    return (latest?.textContent ?? '').trim();
  }

  /** The dialog may portal out of the component, so its rows are looked up on the document. */
  function dialogRows(): HTMLElement[] {
    return Array.from(document.querySelectorAll<HTMLElement>('.row-audit-row'));
  }

  function dialogText(): string {
    return document.querySelector('.p-dialog')?.textContent ?? '';
  }

  function clickBadge(): void {
    const button = (fixture.nativeElement as HTMLElement).querySelector('button');
    button?.click();
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    });
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    fixture?.destroy();
    httpMock.verify();
  });

  // ---------- The inline latest entry ----------

  it('fetches this record by table name and pkid', () => {
    render('Course', 123);

    const request = expectRequest();
    expect(request.request.params.get('tableName')).toBe('Course');
    expect(request.request.params.get('pkid')).toBe('123');
    request.flush(trail);
  });

  it('shows the most recent entry inline without being opened', () => {
    render('Course', 123);
    expectRequest().flush(trail);
    fixture.detectChanges();

    // The head of the list, formatted from local components: no UTC conversion on the way in.
    expect(latestText()).toBe('Update by alice · 2026-06-04 14:30');
    expect(badgeText()).toContain('異動紀錄 History');
  });

  it('renders the bilingual label even while the request is on the wire', () => {
    render('Course', 123);

    expect(badgeText()).toContain('異動紀錄 History');
    expectRequest().flush([]);
  });

  // ---------- The dialog ----------

  it('opens a dialog listing the full trail newest first', () => {
    render('Course', 123);
    expectRequest().flush(trail);
    fixture.detectChanges();

    expect(dialogRows()).toHaveSize(0);

    clickBadge();

    const rows = dialogRows();
    expect(rows).toHaveSize(3);
    expect(rows[0].textContent).toContain('2026-06-04 14:30');
    expect(rows[0].textContent).toContain('alice');
    expect(rows[0].textContent).toContain('Update');
    expect(rows[0].textContent).toContain('Title,DisplayOrder');
    // Oldest last, and the whole trail is there — not just the entry on the badge.
    expect(rows[2].textContent).toContain('Insert');
    expect(rows[2].textContent).toContain('C-001');
  });

  it('does not re-fetch when the dialog is opened', () => {
    render('Course', 123);
    expectRequest().flush(trail);
    fixture.detectChanges();

    clickBadge();

    // The badge already holds the trail; opening it is not a reason to ask again.
    httpMock.expectNone((req) => req.url === baseUrl);
    expect(dialogRows()).toHaveSize(3);
  });

  // ---------- Empty and failed ----------

  it('shows a neutral no-history state on the badge when the record has no trail', () => {
    render('Course', 123);
    expectRequest().flush([]);
    fixture.detectChanges();

    expect(latestText()).toBe(NO_HISTORY_TEXT);
  });

  it('shows a friendly empty state in the dialog when the record has no trail', () => {
    render('Course', 123);
    expectRequest().flush([]);
    fixture.detectChanges();

    clickBadge();

    expect(dialogRows()).toHaveSize(0);
    expect(dialogText()).toContain(NO_HISTORY_TEXT);
  });

  it('asks for nothing at all when the record has no pkid yet', () => {
    // A form in 新增 mode: there is no record to have a history.
    render('Course', null);

    httpMock.expectNone((req) => req.url === baseUrl);
    expect(latestText()).toBe(NO_HISTORY_TEXT);
  });

  it('fetches once the pkid arrives', () => {
    render('Course', null);
    httpMock.expectNone((req) => req.url === baseUrl);

    // A detail page knows its key only after the record has loaded.
    fixture.componentRef.setInput('pkid', 7);
    fixture.detectChanges();

    const request = expectRequest();
    expect(request.request.params.get('pkid')).toBe('7');
    request.flush(trail);
    fixture.detectChanges();

    expect(latestText()).toBe('Update by alice · 2026-06-04 14:30');
  });

  it('says the trail is unavailable rather than empty when the fetch fails', () => {
    render('Course', 123);
    expectRequest().flush(null, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();

    // "No history" would be a claim about the record; this is a claim about the request.
    expect(latestText()).toBe(HISTORY_UNAVAILABLE_TEXT);
  });
});
