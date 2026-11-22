// Adds matchers like toBeInTheDocument() to Vitest's expect.
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Vitest has no global afterEach here, so React Testing Library cannot clean up by itself: unmount after each test.
afterEach(() => cleanup())
