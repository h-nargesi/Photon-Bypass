import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { LocalStorageService } from '../local-storage/local-storage-service';

export const authGuard: CanActivateFn = (_route, state) => {
  const router = inject(Router);
  const token = LocalStorageService.get(['user', 'bearer']);

  if (token?.access_token) return true;

  return router.createUrlTree(['/login'], {
    queryParams: { redirect: state.url },
  });
};
