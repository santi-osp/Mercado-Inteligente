import { Injectable } from '@angular/core';
import gsap from 'gsap';

@Injectable({
  providedIn: 'root'
})
export class MotionService {
  isReducedMotion(): boolean {
    if (typeof window === 'undefined' || !window.matchMedia) return false;
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  }

  animateSearchResults(scope: Element | string, itemSelector = '.product-card'): gsap.Context | null {
    if (this.isReducedMotion()) return null;
    return gsap.context(() => {
      const items = Array.from(document.querySelectorAll(itemSelector)).slice(0, 12);
      if (items.length > 0) {
        gsap.fromTo(
          items,
          { opacity: 0, y: 8 },
          {
            opacity: 1,
            y: 0,
            duration: 0.18,
            ease: 'power1.out',
            stagger: 0.035
          }
        );
      }
    }, scope);
  }

  highlightPriceChange(el: HTMLElement): void {
    if (this.isReducedMotion() || !el) return;
    gsap.fromTo(
      el,
      { backgroundColor: 'var(--mi-primary-soft, #EAF5F0)' },
      { backgroundColor: 'transparent', duration: 0.65, ease: 'power1.out' }
    );
  }

  animateDrawerOpen(drawerEl: HTMLElement): gsap.Context | null {
    if (this.isReducedMotion() || !drawerEl) return null;
    return gsap.context(() => {
      gsap.fromTo(
        drawerEl,
        { opacity: 0, x: 24 },
        { opacity: 1, x: 0, duration: 0.25, ease: 'power2.out' }
      );
    }, drawerEl);
  }

  animateModalOpen(modalEl: HTMLElement): gsap.Context | null {
    if (this.isReducedMotion() || !modalEl) return null;
    return gsap.context(() => {
      gsap.fromTo(
        modalEl,
        { opacity: 0, scale: 0.98 },
        { opacity: 1, scale: 1, duration: 0.18, ease: 'power1.out' }
      );
    }, modalEl);
  }

  animateOptimizerResults(containerEl: HTMLElement): gsap.Context | null {
    if (this.isReducedMotion() || !containerEl) return null;
    return gsap.context(() => {
      const total = containerEl.querySelector('.optimizer-total');
      const buckets = Array.from(containerEl.querySelectorAll('.store-bucket-card'));
      if (total) {
        gsap.fromTo(total, { opacity: 0, y: 8 }, { opacity: 1, y: 0, duration: 0.2, ease: 'power1.out' });
      }
      if (buckets.length > 0) {
        gsap.fromTo(
          buckets,
          { opacity: 0, y: 8 },
          { opacity: 1, y: 0, duration: 0.2, ease: 'power1.out', stagger: 0.05, delay: 0.08 }
        );
      }
    }, containerEl);
  }

  animateToastEnter(toastEl: HTMLElement): gsap.Context | null {
    if (this.isReducedMotion() || !toastEl) return null;
    return gsap.context(() => {
      gsap.fromTo(
        toastEl,
        { opacity: 0, y: 8 },
        { opacity: 1, y: 0, duration: 0.22, ease: 'power1.out' }
      );
    }, toastEl);
  }

  animateEmptyState(el: HTMLElement): gsap.Context | null {
    if (this.isReducedMotion() || !el) return null;
    return gsap.context(() => {
      gsap.fromTo(el, { opacity: 0 }, { opacity: 1, duration: 0.18, ease: 'power1.out' });
    }, el);
  }
}
