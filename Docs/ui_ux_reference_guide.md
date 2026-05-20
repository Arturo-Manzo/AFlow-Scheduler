# Guía de Referencia UI/UX para DeskIQ

Este documento es una guía rápida de referencia para crear vistas homogéneas en DeskIQ. Basado en el UI Design System y la página de detalle de ticket como referencia práctica.

## 📋 Tabla de Contenidos

- [Estructura Base de una Vista](#estructura-base-de-una-vista)
- [Patrones de Layout](#patrones-de-layout)
- [Componentes y Estándares](#componentes-y-estándares)
- [Colores y Variables](#colores-y-variables)
- [Tipografía](#tipografía)
- [Estados de Carga y Error](#estados-de-carga-y-error)
- [Modales y Diálogos](#modales-y-diálogos)
- [Feedback de Usuario](#feedback-de-usuario)
- [Responsividad](#responsividad)
- [Checklist de Implementación](#checklist-de-implementación)

---

## Estructura Base de una Vista

### Plantilla Estándar

```html
<section class="flex flex-col gap-8 pb-8">
  <!-- Header de la página -->
  <div class="flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
    <div class="flex-1">
      <button type="button" class="pl-0 text-sm font-semibold text-[var(--color-accent)] hover:opacity-70 transition-opacity" (click)="backToList()">
        &larr; Volver a [Nombre Vista]
      </button>
      <h1 class="ui-page__title mt-2">[Título de la Vista]</h1>
    </div>
  </div>

  <!-- Feedback global -->
  @if (actionError()) {
    <div class="ui-feedback ui-feedback--error">
      <svg class="ui-feedback__icon" viewBox="0 0 20 20" fill="currentColor">
        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" />
      </svg>
      <span class="text-xs">{{ actionError() }}</span>
    </div>
  }

  @if (actionSuccess()) {
    <div class="ui-feedback ui-feedback--success">
      <svg class="ui-feedback__icon" viewBox="0 0 20 20" fill="currentColor">
        <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" />
      </svg>
      <span class="text-xs">{{ actionSuccess() }}</span>
    </div>
  }

  <!-- Contenido principal -->
  <div class="grid grid-cols-12 gap-6">
    <div class="col-span-12 space-y-6 xl:col-span-8">
      <!-- Contenido principal -->
    </div>
    <aside class="col-span-12 space-y-6 xl:col-span-4">
      <!-- Sidebar -->
    </aside>
  </div>
</section>
```

### Estados de Carga

```html
@if (loading()) {
  <article class="rounded-xl bg-white px-5 py-10 text-sm text-[var(--color-muted)] shadow-[0_8px_24px_rgba(0,61,121,0.06)]">
    Cargando...
  </article>
}
```

### Estados de Error

```html
@else if (error()) {
  <article class="space-y-4 rounded-xl border border-[var(--ui-danger-border)] bg-[var(--ui-danger-bg)] px-5 py-10 text-sm text-[var(--ui-danger-text)]">
    <p>{{ error() }}</p>
    <div class="flex gap-2">
      <button type="button" appButton="danger" buttonSize="md" (click)="backToList()">
        Volver
      </button>
      <button type="button" appButton="primary" buttonSize="md" (click)="retryLoad()">
        Reintentar
      </button>
    </div>
  </article>
}
```

---

## Patrones de Layout

### Layout de 2 Columnas (Principal + Sidebar)

```html
<div class="grid grid-cols-12 gap-6">
  <!-- Columna principal (8/12) -->
  <div class="col-span-12 space-y-6 xl:col-span-8">
    <article class="ui-card ui-card--padded">
      <!-- Contenido -->
    </article>
  </div>

  <!-- Sidebar (4/12) -->
  <aside class="col-span-12 space-y-6 xl:col-span-4">
    <article class="ui-card ui-card--padded">
      <!-- Contenido lateral -->
    </article>
  </aside>
</div>
```

### Layout de 1 Columna (Centrado)

```html
<div class="max-w-4xl mx-auto space-y-6">
  <article class="ui-card ui-card--padded">
    <!-- Contenido -->
  </article>
</div>
```

### Grid de 3 Columnas (Dashboard)

```html
<div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
  <article class="ui-card ui-card--padded">
    <!-- Item 1 -->
  </article>
  <article class="ui-card ui-card--padded">
    <!-- Item 2 -->
  </article>
  <article class="ui-card ui-card--padded">
    <!-- Item 3 -->
  </article>
</div>
```

---

## Componentes y Estándares

### Botones

**Siempre usar `ButtonDirective` del ui-design-system**

```typescript
import { ButtonDirective } from 'ui-design-system';

@Component({
  standalone: true,
  imports: [CommonModule, RouterLink, ButtonDirective],
})
```

**Variantes:**
- `primary` - Acción principal (guardar, crear, confirmar)
- `secondary` - Acción auxiliar (cancelar, editar, ver)
- `danger` - Acción destructiva (eliminar, cancelar)
- `neutral` - Acción neutra
- `info` - Acción informativa
- `warning` - Acción de advertencia

**Tamaños:**
- `sm` - Pequeño (para botones en listas o tablas)
- `md` - Mediano (default, para formularios)
- `lg` - Grande (para CTAs principales)

**Ejemplos:**

```html
<!-- Botón primario -->
<button type="button" appButton="primary" buttonSize="md" [disabled]="working()" (click)="save()">
  Guardar
</button>

<!-- Botón secundario -->
<button type="button" appButton="secondary" buttonSize="sm" [disabled]="working()" (click)="cancel()">
  Cancelar
</button>

<!-- Botón con icono -->
<button type="button" appButton="secondary" buttonSize="sm" [disabled]="working()" (click)="edit()" class="flex items-center gap-1">
  <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z"></path>
  </svg>
  Editar
</button>
```

### Cards

**Card básica:**
```html
<article class="ui-card">
  <!-- Contenido sin padding -->
</article>
```

**Card con padding:**
```html
<article class="ui-card ui-card--padded">
  <!-- Contenido con padding -->
</article>
```

**Card suave (fondo diferente):**
```html
<article class="ui-card ui-card--soft">
  <!-- Contenido con fondo suave -->
</article>
```

### Inputs y Formularios

**Label estándar:**
```html
<label class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--color-muted)]" for="field-name">
  Nombre del Campo
</label>
```

**Input de texto:**
```html
<input
  type="text"
  id="field-name"
  class="ui-input rounded-md"
  [value]="fieldValue()"
  [disabled]="working()"
  (input)="onFieldChange($any($event.target).value)"
/>
```

**Textarea:**
```html
<textarea
  class="ui-input min-h-24 rounded-md"
  [value]="fieldValue()"
  [disabled]="working()"
  placeholder="Escribe aquí..."
  (input)="onFieldChange($any($event.target).value)"
></textarea>
```

**Select:**
```html
<select
  class="ui-input rounded-md text-sm font-semibold"
  [disabled]="working()"
  (change)="onFieldChange($any($event.target).value)"
>
  @for (option of options; track option.value) {
    <option [value]="option.value" [selected]="option.value === selectedValue()">
      {{ option.label }}
    </option>
  }
</select>
```

**Checkbox:**
```html
<label class="flex items-center gap-2 text-xs uppercase tracking-[0.1em] text-[var(--color-muted)]">
  <input
    type="checkbox"
    [checked]="fieldValue()"
    [disabled]="working()"
    (change)="onFieldChange($any($event.target).checked)"
  />
  Etiqueta del checkbox
</label>
```

### Secciones de Información

**Título de sección:**
```html
<p class="text-md font-bold text-[var(--color-accent)]">Título de la Sección</p>
```

**Contenedor de información:**
```html
<div class="mt-3 rounded-lg bg-[var(--color-surface-low)] p-6">
  <p class="text-sm leading-6 text-[var(--color-muted)]">Contenido</p>
</div>
```

**Lista de definiciones (para metadatos):**
```html
<dl class="mt-3 space-y-3 text-xs">
  <div>
    <dt class="text-sm font-bold text-[var(--color-accent)]">Campo 1</dt>
    <dd class="mt-1 text-sm font-semibold">Valor 1</dd>
  </div>
  <div>
    <dt class="text-sm font-bold text-[var(--color-accent)]">Campo 2</dt>
    <dd class="mt-1 text-sm font-semibold">Valor 2</dd>
  </div>
</dl>
```

### Listas

**Lista de items:**
```html
<ul class="mt-3 space-y-2">
  @for (item of items; track item.id) {
    <li class="rounded-lg bg-[var(--color-surface-low)] px-3 py-2 text-xs">
      <p class="font-medium">{{ item.name }}</p>
      <p class="mt-0.5 text-[var(--color-muted)]">{{ item.description }}</p>
    </li>
  }
</ul>
```

**Lista con acciones:**
```html
<ul class="mt-3 space-y-2">
  @for (item of items; track item.id) {
    <li class="rounded-lg bg-[var(--color-surface-low)] px-3 py-2 text-xs">
      <div class="flex items-center justify-between gap-2">
        <div class="flex-1 min-w-0">
          <p class="truncate font-medium">{{ item.name }}</p>
        </div>
        <div class="flex gap-1">
          <button type="button" appButton="secondary" buttonSize="sm" [disabled]="working()" (click)="edit(item)">
            Editar
          </button>
          <button type="button" appButton="danger" buttonSize="sm" [disabled]="working()" (click)="delete(item)">
            Eliminar
          </button>
        </div>
      </div>
    </li>
  }
</ul>
```

### Timeline

```html
<div class="mt-3 rounded-lg bg-[var(--color-surface-low)] p-6">
  <ul class="mt-4 space-y-6 border-l-2 border-[color:color-mix(in_srgb,var(--color-accent)_15%,transparent)] pl-5 text-sm">
    @for (item of timelineItems; track item.id) {
      <li class="relative">
        <span class="absolute -left-[1.38rem] top-0 flex h-3.5 w-3.5 items-center justify-center rounded-full bg-[var(--color-accent)] ring-4 ring-white text-xs">
          {{ item.icon }}
        </span>
        <p class="font-semibold">{{ item.title }}</p>
        <p class="text-xs text-[var(--color-muted)]">{{ item.user }} • {{ item.date }}</p>
      </li>
    }
  </ul>
</div>
```

---

## Colores y Variables

### Colores Principales

```css
--color-bg: #F8F9FB                    /* Fondo principal */
--color-surface: #ffffff               /* Superficie blanca */
--color-surface-low: #f0f4fc           /* Superficie suave (para contenedores de info) */
--color-surface-muted: #e4e8f0         /* Superficie atenuada */
--color-text: #333333                 /* Texto principal */
--color-muted: #5f6672                /* Texto secundario */
--color-accent: #003d79               /* Color de acento (azul) - para títulos y links */
--color-accent-deep: #002a55           /* Acento oscuro */
```

### Colores de Estado UI

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

### Sombras y Bordes

```css
--ui-border-color: #E0E0E0            /* Color de borde */
--ui-card-bg: #ffffff                 /* Fondo de tarjeta */
--ui-card-muted-bg: #f0f4fc           /* Fondo de tarjeta suave */
--ui-shadow-card: 0 8px 24px rgba(0, 61, 121, 0.06)  /* Sombra de tarjeta */
--ui-shadow-card-strong: 0 14px 28px rgba(0, 61, 121, 0.12)  /* Sombra fuerte */
```

### Uso en Clases

```html
<!-- Texto principal -->
<p class="text-[var(--color-text)]">Texto principal</p>

<!-- Texto secundario -->
<p class="text-[var(--color-muted)]">Texto secundario</p>

<!-- Acento (títulos, links) -->
<p class="text-[var(--color-accent)]">Texto de acento</p>

<!-- Fondo suave -->
<div class="bg-[var(--color-surface-low)]">Contenido</div>

<!-- Estado de éxito -->
<span class="bg-[var(--ui-success-bg)] text-[var(--ui-success-text)]">Éxito</span>

<!-- Estado de error -->
<span class="bg-[var(--ui-danger-bg)] text-[var(--ui-danger-text)]">Error</span>
```

---

## Tipografía

### Títulos

```html
<!-- Título de página -->
<h1 class="ui-page__title">Título de la Página</h1>

<!-- Título de sección -->
<p class="text-md font-bold text-[var(--color-accent)]">Título de Sección</p>

<!-- Subtítulo -->
<p class="text-sm font-bold text-[var(--color-accent)]">Subtítulo</p>
```

### Labels y Etiquetas

```html
<!-- Label de formulario -->
<label class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--color-muted)]">
  NOMBRE DEL CAMPO
</label>

<!-- Label de sección -->
<p class="text-xs font-bold uppercase tracking-[0.18em] text-[var(--color-muted)]">
  SECCIÓN
</p>
```

### Texto de Contenido

```html
<!-- Texto normal -->
<p class="text-sm leading-6 text-[var(--color-muted)]">
  Contenido del texto
</p>

<!-- Texto destacado -->
<p class="text-sm font-semibold">Texto destacado</p>

<!-- Texto pequeño -->
<p class="text-xs text-[var(--color-muted)]">Texto pequeño</p>
```

---

## Estados de Carga y Error

### Loading State

```html
@if (loading()) {
  <article class="rounded-xl bg-white px-5 py-10 text-sm text-[var(--color-muted)] shadow-[0_8px_24px_rgba(0,61,121,0.06)]">
    Cargando...
  </article>
}
```

### Error State

```html
@else if (error()) {
  <article class="space-y-4 rounded-xl border border-[var(--ui-danger-border)] bg-[var(--ui-danger-bg)] px-5 py-10 text-sm text-[var(--ui-danger-text)]">
    <p>{{ error() }}</p>
    <div class="flex gap-2">
      <button type="button" appButton="danger" buttonSize="md" (click)="back()">
        Volver
      </button>
      <button type="button" appButton="primary" buttonSize="md" (click)="retry()">
        Reintentar
      </button>
    </div>
  </article>
}
```

### Empty State

```html
@if (!items?.length) {
  <div class="rounded-lg bg-[var(--color-surface-low)] px-3 py-4 text-xs text-[var(--color-muted)]">
    No hay elementos aún.
  </div>
}
```

---

## Modales y Diálogos

### Modal Estándar

```html
@if (modalOpen()) {
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
    <div class="w-full max-w-xl space-y-4 rounded-xl bg-white p-5 shadow-2xl">
      <!-- Header -->
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-semibold uppercase tracking-[0.12em]">Título del Modal</h3>
        <button type="button" class="text-[var(--color-accent)] hover:opacity-70 transition-opacity" (click)="closeModal()">
          Cerrar
        </button>
      </div>

      <!-- Contenido -->
      <div class="space-y-4">
        <!-- Formulario o contenido -->
      </div>

      <!-- Footer con botones -->
      <div class="flex justify-end gap-2">
        <button type="button" appButton="secondary" buttonSize="md" [disabled]="working()" (click)="closeModal()">
          Cancelar
        </button>
        <button type="button" appButton="primary" buttonSize="md" [disabled]="working()" (click)="confirm()">
          Confirmar
        </button>
      </div>
    </div>
  </div>
}
```

### Modal con Formulario

```html
@if (modalOpen()) {
  <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4">
    <div class="w-full max-w-2xl space-y-4 rounded-xl bg-white p-5 shadow-2xl">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-semibold uppercase tracking-[0.12em]">Crear/Editar</h3>
        <button type="button" class="text-[var(--color-accent)] hover:opacity-70 transition-opacity" (click)="closeModal()">
          Cerrar
        </button>
      </div>

      <div class="grid gap-3 sm:grid-cols-2">
        <label class="space-y-1 text-xs uppercase tracking-[0.1em] text-[var(--color-muted)]">
          Campo 1
          <input type="text" class="ui-input rounded-md" [value]="field1()" (input)="onField1Change($any($event.target).value)" />
        </label>

        <label class="space-y-1 text-xs uppercase tracking-[0.1em] text-[var(--color-muted)]">
          Campo 2
          <select class="ui-input rounded-md" [value]="field2()" (change)="onField2Change($any($event.target).value)">
            <option value="">Seleccionar</option>
            @for (option of options; track option.value) {
              <option [value]="option.value">{{ option.label }}</option>
            }
          </select>
        </label>

        <label class="space-y-1 text-xs uppercase tracking-[0.1em] text-[var(--color-muted)] sm:col-span-2">
          Descripción
          <textarea class="ui-input min-h-20 rounded-md" [value]="description()" (input)="onDescriptionChange($any($event.target).value)"></textarea>
        </label>
      </div>

      <div class="flex justify-end gap-2">
        <button type="button" appButton="secondary" buttonSize="md" [disabled]="working()" (click)="closeModal()">
          Cancelar
        </button>
        <button type="button" appButton="primary" buttonSize="md" [disabled]="working()" (click)="save()">
          Guardar
        </button>
      </div>
    </div>
  </div>
}
```

---

## Feedback de Usuario

### Mensaje de Éxito

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

### Mensaje de Error

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

### Badges de Estado

```html
<!-- Badge de éxito -->
<span class="inline-flex items-center rounded-full bg-[var(--ui-success-bg)] px-2 py-0.5 text-xs font-medium text-[var(--ui-success-text)]">
  Activo
</span>

<!-- Badge de advertencia -->
<span class="inline-flex items-center rounded-full bg-[var(--ui-warning-bg)] px-2 py-0.5 text-xs font-medium text-[var(--ui-warning-text)]">
  Pendiente
</span>

<!-- Badge de error -->
<span class="inline-flex items-center rounded-full bg-[var(--ui-danger-bg)] px-2 py-0.5 text-xs font-medium text-[var(--ui-danger-text)]">
  Error
</span>
```

---

## Responsividad

### Breakpoints

- `sm`: 640px
- `md`: 768px
- `lg`: 1024px
- `xl`: 1280px

### Patrones Responsivos

**Grid de 2 columnas:**
```html
<div class="grid gap-6 md:grid-cols-2">
  <!-- 1 columna en móvil, 2 en tablet+ -->
</div>
```

**Layout principal/sidebar:**
```html
<div class="grid grid-cols-12 gap-6">
  <div class="col-span-12 xl:col-span-8">
    <!-- Full width en móvil, 8/12 en desktop -->
  </div>
  <aside class="col-span-12 xl:col-span-4">
    <!-- Full width en móvil, 4/12 en desktop -->
  </aside>
</div>
```

**Botón FAB móvil:**
```html
<!-- Solo visible en móvil -->
<button class="lg:hidden fixed bottom-6 right-6 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-[var(--color-accent)] text-white shadow-lg">
  <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
  </svg>
</button>
```

**Texto responsive:**
```html
<p class="text-xs md:text-sm lg:text-base">
  Texto que crece con el viewport
</p>
```

---

## Checklist de Implementación

Antes de considerar una vista como completa, verifica:

### ✅ Estructura
- [ ] Usa `<section class="flex flex-col gap-8 pb-8">` como contenedor principal
- [ ] Incluye header con botón de navegación y título
- [ ] Usa grid de 12 columnas para layouts de 2 columnas
- [ ] Usa `ui-card` y `ui-card--padded` para contenedores

### ✅ Componentes
- [ ] Todos los botones usan `ButtonDirective`
- [ ] Todos los inputs usan clase `ui-input`
- [ ] Todos los labels usan el estilo estándar (uppercase, tracking)
- [ ] Usa `ui-feedback` para mensajes de éxito/error

### ✅ Colores y Variables
- [ ] Usa variables CSS para colores (`--color-*`, `--ui-*`)
- [ ] Usa `--color-accent` para títulos y links
- [ ] Usa `--color-muted` para texto secundario
- [ ] Usa `--color-surface-low` para contenedores de información

### ✅ Tipografía
- [ ] Usa `ui-page__title` para título de página
- [ ] Usa `text-md font-bold text-[var(--color-accent)]` para títulos de sección
- [ ] Usa `text-xs font-bold uppercase tracking-[0.18em]` para labels
- [ ] Usa `text-sm leading-6 text-[var(--color-muted)]` para contenido

### ✅ Estados
- [ ] Implementa estado de carga
- [ ] Implementa estado de error
- [ ] Implementa empty state cuando corresponda
- [ ] Deshabilita botones durante operaciones con `[disabled]="working()"`

### ✅ Responsividad
- [ ] Layout funciona en móvil (1 columna)
- [ ] Layout funciona en tablet (2 columnas)
- [ ] Layout funciona en desktop (layout completo)
- [ ] Botones FAB para acciones principales en móvil cuando sea necesario

### ✅ Accesibilidad
- [ ] Todos los inputs tienen label asociado
- [ ] Los botones tienen texto descriptivo
- [ ] Los links tienen texto descriptivo
- [ ] Los modales pueden cerrarse con ESC (considerar implementar)

### ✅ UX
- [ ] Feedback inmediato para acciones del usuario
- [ ] Confirmación para acciones destructivas
- [ ] Indicadores de carga durante operaciones asíncronas
- [ ] Mensajes de error claros y accionables

---

## Referencias

- Documentación del UI Design System: `Docs/UI_DESIGN_SYSTEM_GUIDE.md`
- Ejemplo de implementación: `src/deskiq-client/src/app/features/tickets/ticket-detail-page.component.html`
- TypeScript de referencia: `src/deskiq-client/src/app/features/tickets/ticket-detail-page.component.ts`
- Estilos fuente: `.git/modules/ui-design-system/projects/ui-design-system/src/lib/styles/ui-design-system.scss`

---

## Notas Importantes

1. **Siempre usa el UI Design System**: No crees estilos personalizados si existe una clase utilitaria equivalente
2. **Consistencia es clave**: Si un patrón funciona en una vista, reúsalo en otras
3. **Variables CSS sobre valores hardcoded**: Usa variables para colores y espaciados
4. **Signals para estado reactivo**: Usa signals de Angular para estado del componente
5. **ButtonDirective es obligatorio**: No uses estilos CSS directos en botones
6. **ui-feedback para mensajes**: No creas estilos personalizados para alertas
7. **Responsividad first**: Diseña pensando en móvil primero, luego escala a desktop
