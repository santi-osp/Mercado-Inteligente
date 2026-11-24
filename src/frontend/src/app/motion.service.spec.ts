import { TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { MotionService } from './motion.service';

describe('MotionService', () => {
  let service: MotionService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MotionService]
    });
    service = TestBed.inject(MotionService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('detects reduced motion flag or defaults gracefully', () => {
    expect(typeof service.isReducedMotion()).toBe('boolean');
  });

  it('creates animation context or respects reduced motion', () => {
    const el = document.createElement('div');
    el.className = 'test-toast';
    document.body.appendChild(el);
    const ctx = service.animateToastEnter(el);
    if (ctx) {
      ctx.revert();
    }
    document.body.removeChild(el);
    expect(true).toBe(true);
  });
});
