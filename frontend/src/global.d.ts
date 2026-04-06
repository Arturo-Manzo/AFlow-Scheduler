export {};

declare global {
  interface CHRONIQRuntimeConfig {
    backendUrl?: string;
  }

  var __CHRONIQ_RUNTIME_CONFIG__: CHRONIQRuntimeConfig | undefined;
}
