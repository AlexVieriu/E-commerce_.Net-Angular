import { beforeEach, describe, expect, it } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { HeaderComponent } from './header.component';
import { BusyService } from '../../core/services/busy.service';

// BUG-10 was the view never hearing that the request finished, so every case waits
// on Angular's own scheduler (whenStable) instead of forcing a refresh — detectChanges() would pass on the broken code too.
describe('HeaderComponent loading bar', () => {
  let fixture: ComponentFixture<HeaderComponent>;
  let busyService: BusyService;

  const barIsVisible = () => fixture.nativeElement.querySelector('mat-progress-bar') !== null;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [HeaderComponent],
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    fixture = TestBed.createComponent(HeaderComponent);
    busyService = TestBed.inject(BusyService);
    await fixture.whenStable();
  });

  it('stays hidden while nothing is in flight', () => {
    expect(barIsVisible()).toBe(false);
  });

  it('appears once a request starts', async () => {
    busyService.busy();
    await fixture.whenStable();

    expect(barIsVisible()).toBe(true);
  });

  // The regression: no click follows the response, so only the signal can trigger the re-render.
  it('disappears when the request finishes', async () => {
    busyService.busy();
    await fixture.whenStable();

    busyService.idle();
    await fixture.whenStable();

    expect(barIsVisible()).toBe(false);
  });

  it('stays while a second request is still in flight', async () => {
    busyService.busy();
    busyService.busy();
    busyService.idle();
    await fixture.whenStable();

    expect(barIsVisible()).toBe(true);
  });

  // An unbalanced idle() must not push the count negative, or the next request would not show the bar.
  it('shows the bar again after an extra idle', async () => {
    busyService.idle();
    busyService.busy();
    await fixture.whenStable();

    expect(barIsVisible()).toBe(true);
  });
});
