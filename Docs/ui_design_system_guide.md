# Guía de Implementación del UI Design System

Este documento explica cómo utilizar e implementar el **ui-design-system** en las vistas de DeskIQ, utilizando la vista de detalle de ticket (`ticket-detail-page`) como referencia práctica.

## ¿Qué es el ui-design-system?

El **ui-design-system** es una librería Angular independiente que centraliza el sistema de diseño visual estándar para todas las aplicaciones. Proporciona:

- Variables y utilidades CSS globales (modo claro/oscuro)
- Directivas para uniformidad visual (ej: `ButtonDirective`)
- Servicio para gestión de temas (`ThemeService`)
- Clases CSS utilitarias predefinidas

## Instalación y Configuración

### 1. Importar estilos globales

En tu archivo `src/styles.scss`:

```scss
@import 'ui-design-system/styles/ui-design-system.scss';
```

Esto habilita todas las variables, utilidades y estilos base UI en tu aplicación.

### 2. Importar directivas en componentes

En tu componente TypeScript:

```typescript
import { ButtonDirective } from 'ui-design-system';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonDirective],
  // ...
})
export class YourComponent {
  // ...
}
```

## Componentes del UI Design System

### 1. ButtonDirective

Directiva para estandarizar el estilo y comportamiento de los botones.

**Variantes disponibles:**
- `primary` - Botón principal (acciones importantes)
- `secondary` - Botón secundario (acciones auxiliares)
- `neutral` - Botón neutro
- `info` - Botón informativo
- `warning` - Botón de advertencia
- `danger` - Botón de peligro (acciones destructivas)

**Tamaños disponibles:**
- `sm` - Pequeño
- `md` - Mediano (por defecto)
- `lg` - Grande

**Ejemplo de uso en ticket-detail-page.component.html:**

```html
<!-- Botón primario para acciones principales -->
<button
  type="button"
  appButton="primary"
  buttonSize="md"
  [disabled]="working() || !comment().trim()"
  (click)="addComment()">
  Publicar Comentario
</button>

<!-- Botón secundario para acciones auxiliares -->
<button
  type="button"
  appButton="secondary"
  buttonSize="sm"
  [disabled]="working()"
  (click)="openEditModal()">
  Editar
</button>

<!-- Botón de peligro para acciones destructivas -->
<button
  type="button"
  appButton="danger"
  buttonSize="md"
  (click)="backToList()">
  Volver a tickets
</button>
```

**En ticket-detail-page.component.ts:**

```typescript
import { ButtonDirective } from 'ui-design-system';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonDirective],
  // ...
})
export class TicketDetailPageComponent {
  // ...
}
```

### 2. Clases CSS Utilitarias

#### Estructura de Página

**`.ui-page`** - Contenedor principal de la página

```html
<section class="ui-page">
  <!-- Contenido de la página -->
</section>
```

**`.ui-page__title`** - Título principal de la página

```html
<h1 class="ui-page__title">Ticket {{ current.ticketId || current.id }}</h1>
```

#### Tarjetas (Cards)

**`.ui-card`** - Tarjeta base con sombra

```html
<article class="ui-card">
  <!-- Contenido -->
</article>
```

**`.ui-card--padded`** - Tarjeta con padding interno

```html
<article class="ui-card ui-card--padded">
  <div class="grid gap-8 md:grid-cols-3">
    <!-- Contenido con padding -->
  </div>
</article>
```

**`.ui-card--soft`** - Tarjeta con fondo suave

```html
<article class="ui-card ui-card--soft">
  <!-- Contenido con fondo suave -->
</article>
```

#### Formularios e Inputs

**`.ui-form-label`** - Etiqueta de formulario

```html
<label class="ui-form-label text-xs font-bold uppercase tracking-[0.2em] text-[var(--color-muted)]" for="ticket-search">
  Buscar
</label>
```

**`.ui-input`** - Input estandarizado

```html
<input
  type="text"
  class="ui-input rounded-md"
  [value]="editTitle()"
  [disabled]="working()"
  (input)="editTitle.set($any($event.target).value)"
/>
```

**Textarea con estilo ui-input:**

```html
<textarea
  class="ui-input min-h-24"
  [value]="comment()"
  [disabled]="working()"
  placeholder="Escribe un comentario..."
  (input)="onCommentChange($any($event.target).value)"
></textarea>
```

**Select con estilo ui-input:**

```html
<select
  class="ui-input rounded-md text-sm font-semibold"
  [disabled]="working()"
  (change)="onStatusChange($any($event.target).value)"
>
  @for (status of statuses; track status.value) {
    <option [value]="status.value" [selected]="status.value === selectedStatus()">
      {{ status.label }}
    </option>
  }
</select>
```

#### Feedback y Alertas

**`.ui-feedback`** - Contenedor de feedback base

**`.ui-feedback--error`** - Feedback de error

```html
@if (actionError()) {
  <div class="ui-feedback ui-feedback--error">
    <svg class="ui-feedback__icon" viewBox="0 0 20 20" fill="currentColor">
      <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
    </svg>
    <span class="text-xs">{{ actionError() }}</span>
  </div>
}
```

**`.ui-feedback--success`** - Feedback de éxito

```html
@if (actionSuccess()) {
  <div class="ui-feedback ui-feedback--success">
    <svg class="ui-feedback__icon" viewBox="0 0 20 20" fill="currentColor">
      <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
    </svg>
    <span class="text-xs">{{ actionSuccess() }}</span>
  </div>
}
```

**`.ui-feedback__icon`** - Icono del feedback

#### Tablas

**`.ui-table-wrap`** - Contenedor con scroll horizontal

```html
<div class="ui-table-wrap">
  <table class="ui-table">
    <!-- Contenido de la tabla -->
  </table>
</div>
```

**`.ui-table`** - Tabla estandarizada

```html
<table class="ui-table">
  <thead>
    <tr>
      <th>Columna 1</th>
      <th>Columna 2</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>Dato 1</td>
      <td>Dato 2</td>
    </tr>
  </tbody>
</table>
```

#### KPI Cards

**`.ui-kpi-grid`** - Grid responsive para KPIs

```html
<div class="ui-kpi-grid">
  <div class="ui-kpi-card">
    <p class="ui-kpi-label">Total Tickets</p>
    <p class="ui-kpi-value">150</p>
  </div>
</div>
```

**`.ui-kpi-card`** - Tarjeta de KPI

**`.ui-kpi-label`** - Etiqueta del KPI

**`.ui-kpi-value`** - Valor del KPI

## Variables CSS Disponibles

### Colores Principales

```css
--color-bg: #F8F9FB                    /* Fondo principal */
--color-surface: #ffffff               /* Superficie blanca */
--color-surface-low: #f0f4fc           /* Superficie suave */
--color-surface-muted: #e4e8f0         /* Superficie atenuada */
--color-text: #333333                 /* Texto principal */
--color-muted: #5f6672                /* Texto secundario */
--color-accent: #003d79               /* Color de acento (azul) */
--color-accent-deep: #002a55           /* Acento oscuro */
```

### Colores de UI

```css
--ui-danger-bg: #FEF2F2                /* Fondo error */
--ui-danger-border: #FECACA           /* Borde error */
--ui-danger-text: #DC2626             /* Texto error */
--ui-success-bg: #E6F7E6              /* Fondo éxito */
--ui-success-border: #4CAF50          /* Borde éxito */
--ui-success-text: #2E7D32            /* Texto éxito */
--ui-critical-bg: #FFEBEE             /* Fondo crítico */
--ui-critical-border: #EE0000         /* Borde crítico */
--ui-critical-text: #EE0000           /* Texto crítico */
--ui-warning-bg: #FEF3C7             /* Fondo advertencia */
--ui-warning-border: #FCD34D          /* Borde advertencia */
--ui-warning-text: #D97706            /* Texto advertencia */
--ui-info-bg: #E3F2FD                /* Fondo info */
--ui-info-text: #1976D2              /* Texto info */
```

### Bordes y Sombras

```css
--ui-border-color: #E0E0E0            /* Color de borde */
--ui-border-soft: color-mix(in srgb, #E0E0E0 16%, transparent)  /* Borde suave */
--ui-card-bg: #ffffff                 /* Fondo de tarjeta */
--ui-card-muted-bg: #f0f4fc           /* Fondo de tarjeta suave */
--ui-shadow-card: 0 8px 24px rgba(0, 61, 121, 0.06)  /* Sombra de tarjeta */
--ui-shadow-card-strong: 0 14px 28px rgba(0, 61, 121, 0.12)  /* Sombra fuerte */
```

### Radios de Borde

```css
--ui-radius-sm: 0.5rem                /* Radio pequeño */
--ui-radius-md: 0.375rem              /* Radio mediano */
--ui-radius-lg: 0.5rem                /* Radio grande */
```

### SAP Variables (compatibilidad)

```css
--sapFontFamily: 'Inter', sans-serif  /* Fuente */
--sapField_Background: #ffffff        /* Fondo de campo */
--sapField_BorderColor: #E0E0E0       /* Borde de campo */
--sapField_TextColor: #333333         /* Texto de campo */
```

## Uso de Variables CSS en Componentes

En ticket-detail-page.component.ts, se usan variables CSS para estilos dinámicos:

```typescript
getParentTicketStatusClass(status: number): { [key: string]: boolean } {
  if (status === 4 || status === 5) {
    return {
      'bg-[var(--ui-success-bg)]': true,
      'text-[var(--ui-success-text)]': true
    };
  } else if (status === 2) {
    return {
      'bg-[var(--ui-warning-bg)]': true,
      'text-[var(--ui-warning-text)]': true
    };
  } else {
    return {
      'bg-[var(--ui-info-bg)]': true,
      'text-[var(--ui-info-text)]': true
    };
  }
}
```

En HTML:

```html
<span class="inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold"
      [ngClass]="getParentTicketStatusClass(current.parentTicket.status)">
  {{ getStatusLabel(current.parentTicket.status) }}
</span>
```

## ThemeService (Gestión de Temas)

El `ThemeService` permite gestionar el modo claro/oscuro/sistema de forma centralizada.

### Inicialización

En tu `AppComponent`:

```typescript
import { ThemeService } from 'ui-design-system';

@Component({
  // ...
})
export class AppComponent {
  constructor(private themeService: ThemeService) {}

  ngOnInit() {
    this.themeService.init();
  }
}
```

### Cambio de Tema

```typescript
themeService.setMode('dark' | 'light' | 'system');
```

El servicio:
- Guarda la preferencia en localStorage
- Aplica clases `theme-light` o `theme-dark` al elemento `<html>`
- Respeta la preferencia del sistema cuando está en modo `system`
- Expone signals `mode` y `activeTheme` para reactividad

## Ejemplo Completo: ticket-detail-page

### TypeScript (ticket-detail-page.component.ts)

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ButtonDirective } from 'ui-design-system';

@Component({
  selector: 'app-ticket-detail-page',
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonDirective],
  templateUrl: './ticket-detail-page.component.html',
})
export class TicketDetailPageComponent {
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);
  readonly comment = signal('');
  // ... más signals y lógica
}
```

### HTML (ticket-detail-page.component.html)

```html
<section class="flex flex-col gap-8 pb-8">
  @if (loading()) {
    <article class="rounded-xl bg-white px-5 py-10 text-sm text-[var(--color-muted)] shadow-[0_8px_24px_rgba(0,61,121,0.06)]">
      Cargando detalle del ticket...
    </article>
  } @else if (error()) {
    <article class="space-y-4 rounded-xl border border-[var(--ui-danger-border)] bg-[var(--ui-danger-bg)] px-5 py-10 text-sm text-[var(--ui-danger-text)]">
      <p>{{ error() }}</p>
      <div class="flex gap-2">
        <button type="button" appButton="danger" buttonSize="md" (click)="backToList()">
          Volver a tickets
        </button>
      </div>
    </article>
  } @else if (ticket(); as current) {
    <!-- Header de la página -->
    <div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
      <div class="flex-1">
        <button type="button" class="pl-0 text-sm font-semibold text-[var(--color-accent)]" (click)="backToList()">
          &larr; Volver a Tickets
        </button>
        <h1 class="ui-page__title mt-2">Ticket {{ current.ticketId || current.id }}</h1>
      </div>
    </div>

    <!-- Feedback de éxito/error -->
    @if (actionError()) {
      <div class="ui-feedback ui-feedback--error">
        <svg class="ui-feedback__icon" viewBox="0 0 20 20" fill="currentColor">
          <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
        </svg>
        <span class="text-xs">{{ actionError() }}</span>
      </div>
    }

    <!-- Card principal -->
    <article class="ui-card ui-card--padded">
      <div class="grid gap-8 md:grid-cols-3">
        <div>
          <p class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--color-muted)]">Status</p>
          <p class="mt-2 inline-flex items-center gap-2 text-sm font-bold text-[var(--color-text)]">
            <span class="inline-block h-2.5 w-2.5 animate-pulse rounded-full bg-[var(--ui-success-text)]"></span>
            {{ getStatusLabel(current.status) }}
          </p>
        </div>
      </div>

      <!-- Input de comentario -->
      <div class="bg-[var(--color-surface-low)] p-4">
        <textarea
          class="ui-input min-h-24"
          [value]="comment()"
          [disabled]="working()"
          placeholder="Escribe un comentario..."
          (input)="onCommentChange($any($event.target).value)"
        ></textarea>

        <div class="mt-3 flex justify-end">
          <button type="button" appButton="primary" buttonSize="md" [disabled]="working() || !comment().trim()" (click)="addComment()">
            Publicar Comentario
          </button>
        </div>
      </div>
    </article>
  }
</section>
```

## Mejores Prácticas

1. **Siempre usa las clases utilitarias del ui-design-system** en lugar de estilos personalizados
2. **Usa `ButtonDirective` para todos los botones** para mantener consistencia visual
3. **Aprovecha las variables CSS** para colores y espaciados en lugar de valores hardcoded
4. **Usa `ui-feedback` para mensajes de error/éxito** en lugar de crear estilos personalizados
5. **Mantén la consistencia en el uso de variantes de botones** según el contexto de la acción
6. **Usa `ui-card` y `ui-card--padded`** para contenedores de contenido
7. **Usa `ui-input` para todos los inputs, selects y textareas** para consistencia

## Referencias

- Archivo de referencia: `src/app/features/tickets/ticket-detail-page.component.ts`
- Plantilla de referencia: `src/app/features/tickets/ticket-detail-page.component.html`
- Documentación oficial: `node_modules/ui-design-system/README.md`
- Estilos fuente: `node_modules/ui-design-system/projects/ui-design-system/src/lib/styles/ui-design-system.scss`
