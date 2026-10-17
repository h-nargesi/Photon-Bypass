import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountRole, UserModel } from '../../@models';
import { UserService } from './user-servcie';

/// Note: every injectable is captured synchronously — an async continuation
/// no longer runs inside the injection context.
async function loadUser(): Promise<UserModel | undefined> {
  const users = inject(UserService);

  try {
    return await users.user();
  } catch {
    return undefined;
  }
}

/// Manager-area pages (user management) — Manager or Admin (docs/20 §3).
export const managerGuard: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const user = await loadUser();

  if (!user) {
    return router.createUrlTree(['/login'], {
      queryParams: { redirect: state.url },
    });
  }

  return (user.role ?? AccountRole.User) >= AccountRole.Manager
    ? true
    : router.createUrlTree(['/dashboard']);
};

/// Admin-only pages (server CRUD, wallet adjust) — docs/20 §3.
export const adminGuard: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const user = await loadUser();

  if (!user) {
    return router.createUrlTree(['/login'], {
      queryParams: { redirect: state.url },
    });
  }

  return user.role === AccountRole.Admin
    ? true
    : router.createUrlTree(['/dashboard']);
};

/// Self financial pages (renewal/payment) — dependent accounts are
/// refused server-side with 403 (D6); the guard mirrors that early.
export const selfServiceGuard: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  const user = await loadUser();

  if (!user) {
    return router.createUrlTree(['/login'], {
      queryParams: { redirect: state.url },
    });
  }

  return user.allowSelfService === true
    ? true
    : router.createUrlTree(['/dashboard']);
};
