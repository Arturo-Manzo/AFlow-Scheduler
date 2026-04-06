import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: '**',
    // For IIS static hosting, render everything on the client.
    renderMode: RenderMode.Client,
  },
];
