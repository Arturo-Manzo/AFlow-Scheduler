export {};

declare global {
  interface CHRONIQRuntimeConfig {
    backendUrl?: string;
    port?: number;
  }

  var __CHRONIQ_RUNTIME_CONFIG__: CHRONIQRuntimeConfig | undefined;
}
