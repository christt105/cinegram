<template>
  <Transition name="scroll-top-fade">
    <button
      v-show="isVisible"
      @click="scrollToTop"
      class="scroll-to-top-btn"
      type="button"
      aria-label="Volver arriba"
      title="Volver arriba"
    >
      <ArrowUp :size="22" />
    </button>
  </Transition>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue';
import { ArrowUp } from 'lucide-vue-next';

const props = withDefaults(
  defineProps<{
    target?: HTMLElement | null;
    threshold?: number;
  }>(),
  {
    target: null,
    threshold: 200,
  }
);

const isVisible = ref(false);

const handleScroll = () => {
  const targetScroll = props.target ? props.target.scrollTop : 0;
  const windowScroll = window.scrollY || document.documentElement.scrollTop || 0;
  const currentScroll = Math.max(targetScroll, windowScroll);
  isVisible.value = currentScroll > props.threshold;
};

const scrollToTop = () => {
  if (props.target) {
    props.target.scrollTo({
      top: 0,
      behavior: 'smooth',
    });
  }
  window.scrollTo({
    top: 0,
    behavior: 'smooth',
  });
};

let activeElement: HTMLElement | null = null;

const attachListener = (el: HTMLElement | null) => {
  if (activeElement) {
    activeElement.removeEventListener('scroll', handleScroll);
  }
  activeElement = el;
  if (activeElement) {
    activeElement.addEventListener('scroll', handleScroll, { passive: true });
  }
};

watch(
  () => props.target,
  (newTarget) => {
    attachListener(newTarget);
    handleScroll();
  },
  { immediate: true }
);

onMounted(() => {
  window.addEventListener('scroll', handleScroll, { passive: true });
  handleScroll();
});

onUnmounted(() => {
  window.removeEventListener('scroll', handleScroll);
  if (activeElement) {
    activeElement.removeEventListener('scroll', handleScroll);
  }
});
</script>

<style scoped>
.scroll-to-top-btn {
  position: fixed;
  bottom: 32px;
  right: 32px;
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: var(--surface-container-high, rgba(32, 31, 31, 0.85));
  backdrop-filter: blur(12px);
  -webkit-backdrop-filter: blur(12px);
  border: 1px solid var(--glass-border-hover, rgba(237, 177, 255, 0.3));
  color: var(--primary, #edb1ff);
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  z-index: 999;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4), 0 0 15px rgba(237, 177, 255, 0.15);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  outline: none;
}

.scroll-to-top-btn:hover {
  background: var(--primary-container, #9d50bb);
  color: var(--on-primary-container, #fff3fd);
  border-color: var(--primary, #edb1ff);
  transform: translateY(-4px) scale(1.06);
  box-shadow: 0 12px 28px rgba(157, 80, 187, 0.5), 0 0 20px rgba(237, 177, 255, 0.35);
}

.scroll-to-top-btn:active {
  transform: translateY(-1px) scale(0.96);
  box-shadow: 0 4px 12px rgba(157, 80, 187, 0.4);
}

.scroll-top-fade-enter-active,
.scroll-top-fade-leave-active {
  transition: all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}

.scroll-top-fade-enter-from,
.scroll-top-fade-leave-to {
  opacity: 0;
  transform: translateY(20px) scale(0.7);
}

@media (max-width: 768px) {
  .scroll-to-top-btn {
    bottom: 88px;
    right: 20px;
    width: 44px;
    height: 44px;
  }
}
</style>
