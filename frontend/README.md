# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.4.

## UI Design System Integration

This frontend uses the **ui-design-system** library (v1.0.0) for consistent visual design across the application.

### Installation

The ui-design-system is installed from GitHub:

```json
"ui-design-system": "github:Arturo-Manzo/ui-design-system#v1.0.0"
```

### Current Implementation Status

**Completed Phases:**
- ✅ Phase 1: Configuration - ui-design-system integrated as git submodule
- ✅ Phase 2.1: Template separation for shared components (7 components)
- ✅ Phase 2.2: Template separation for page components (10 components)
- ✅ Phase 3: Component migration to ui-design-system directives (ButtonDirective)
- ✅ Phase 4: ThemeService implementation
- ✅ Phase 5: Global styles update
- ✅ Phase 6: Documentation update
- ✅ Phase 7: Testing and validation

**Integration Method:**

ui-design-system is integrated as a **git submodule** located at `frontend/projects/ui-design-system/`. This approach:
- Allows versioned updates via git
- Avoids npm publication dependency
- Enables AOT compilation
- Maintains separation of concerns

**Lint Warnings:**

There are static analysis warnings about "imports must be an array of components, directives, pipes, or NgModules. Value could not be determined statically." These are IDE/linter warnings that do not affect the build or runtime behavior. The application compiles and runs successfully.

### Template Separation

All component templates have been separated from TypeScript files into dedicated HTML files:

**Shared Components:**
- status-badge
- toast
- confirm-modal
- error-modal
- task-logs-modal
- task-table
- department-list

**Page Components:**
- dashboard
- box-detail
- box-run-detail
- box-run-list
- tasks
- task-detail
- users
- notification-settings
- login

### Next Steps

**To update ui-design-system to a new version:**

```bash
cd frontend/projects/ui-design-system
git fetch origin
git checkout v1.2.0  # or the desired tag/commit
cd ../..
git add projects/ui-design-system
git commit -m "Update ui-design-system to v1.2.0"
```

**Current Implementation:**

- ✅ `ButtonDirective` imported in all components
- ✅ `ThemeService` initialized in app.ts
- ✅ Styles imported from local submodule
- ✅ All components use `appButton` directive in HTML templates
- ✅ Application builds and runs successfully

See `../Docs/ui_design_system_guide.md` for detailed implementation guidance.

**Note:** There is a Sass deprecation warning about `@import` rules. This is a warning from Dart Sass 3.0.0 and does not affect functionality. The ui-design-system library uses legacy Sass imports.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Runtime Configuration

Production deployments can override frontend connectivity without rebuilding the app.

1. Edit `public/config.json` before build, or edit `dist/frontend/browser/config.json` after build.
2. Set `backendUrl` to the backend API base URL.

Example:

```json
{
	"backendUrl": "http://localhost:5000/api"
}
```

Fallback behavior:

- If `config.json` is missing or invalid, the app uses `/api`.
- Trailing slash in `backendUrl` is automatically removed.

## IIS Hosting (Static SPA)

Use this flow to host Angular frontend directly in IIS without SSR.

1. Build static output (default build):

```bash
npm run build
```

Equivalent explicit command:

```bash
npm run build:iis
```

2. Publish the contents of `dist/frontend/browser` to your IIS site root.
3. Ensure URL Rewrite module is installed in IIS.
4. Keep the generated `web.config` file in the site root so deep links (`/dashboard`, `/boxes/123`, etc.) resolve to `index.html`.

Notes:

- `public/web.config` is copied automatically into build output.
- If IIS hosts the frontend under a virtual directory (for example `/scheduler/`), build with matching base href:

```bash
ng build --configuration iis --base-href /scheduler/
```

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
