/**
 * 图片编码属性测试
 * 
 * Property 6: 图片编码正确性
 * Validates: Requirements 4.3, 4.4
 * 
 * Feature: doc-chat-assistant, Property 6: 图片编码正确性
 */
import { describe, it, expect } from 'vitest'
import * as fc from 'fast-check'
import {
  validateImageFile,
  validateBase64Image,
  getMimeTypeFromBase64,
  SUPPORTED_IMAGE_TYPES,
  MAX_IMAGE_SIZE,
} from '../image-upload'

// 支持的MIME类型
const supportedMimeTypes = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'] as const

// 不支持的MIME类型
const unsupportedMimeTypes = [
  'image/bmp',
  'image/tiff',
  'image/svg+xml',
  'application/pdf',
  'text/plain',
  'video/mp4',
]

// 生成有效的Base64图片字符串
const validBase64ImageArb = fc.constantFrom(...supportedMimeTypes).map(mimeType => {
  // 生成一个最小的有效图片数据
  const minimalImageData = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=='
  return `data:${mimeType};base64,${minimalImageData}`
})

// 生成无效的Base64图片字符串（不支持的格式）
const invalidBase64ImageArb = fc.constantFrom(...unsupportedMimeTypes).map(mimeType => {
  const minimalData = 'SGVsbG8gV29ybGQ='
  return `data:${mimeType};base64,${minimalData}`
})

// 生成有效文件大小（小于10MB）
const validFileSizeArb = fc.integer({ min: 1, max: MAX_IMAGE_SIZE - 1 })

// 生成无效文件大小（大于10MB）
const invalidFileSizeArb = fc.integer({ min: MAX_IMAGE_SIZE + 1, max: MAX_IMAGE_SIZE * 2 })

// 创建模拟File对象
function createMockFile(type: string, size: number): File {
  const content = new Uint8Array(size)
  const blob = new Blob([content], { type })
  return new File([blob], 'test-image', { type })
}

describe('ImageUpload Property Tests', () => {
  /**
   * Property 6: 图片编码正确性
   * 
   * 对于任意包含图片的消息，图片必须被正确编码为Base64格式，
   * 且格式必须是PNG、JPG、GIF或WebP之一
   */
  describe('Property 6: 图片编码正确性', () => {
    it('支持的图片格式应该通过验证', () => {
      fc.assert(
        fc.property(
          fc.constantFrom(...supportedMimeTypes),
          validFileSizeArb,
          (mimeType, size) => {
            const file = createMockFile(mimeType, size)
            const result = validateImageFile(file)
            
            expect(result.valid).toBe(true)
            expect(result.error).toBeUndefined()
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('不支持的图片格式应该被拒绝', () => {
      fc.assert(
        fc.property(
          fc.constantFrom(...unsupportedMimeTypes),
          validFileSizeArb,
          (mimeType, size) => {
            const file = createMockFile(mimeType, size)
            const result = validateImageFile(file)
            
            expect(result.valid).toBe(false)
            expect(result.error).toBeDefined()
            expect(result.error).toContain('不支持的图片格式')
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('超过大小限制的图片应该被拒绝', () => {
      fc.assert(
        fc.property(
          fc.constantFrom(...supportedMimeTypes),
          invalidFileSizeArb,
          (mimeType, size) => {
            const file = createMockFile(mimeType, size)
            const result = validateImageFile(file)
            
            expect(result.valid).toBe(false)
            expect(result.error).toBeDefined()
            expect(result.error).toContain('超过限制')
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('有效的Base64图片应该通过验证', () => {
      fc.assert(
        fc.property(
          validBase64ImageArb,
          (base64) => {
            const result = validateBase64Image(base64)
            
            expect(result.valid).toBe(true)
            expect(result.error).toBeUndefined()
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('无效格式的Base64图片应该被拒绝', () => {
      fc.assert(
        fc.property(
          invalidBase64ImageArb,
          (base64) => {
            const result = validateBase64Image(base64)
            
            expect(result.valid).toBe(false)
            expect(result.error).toBeDefined()
            expect(result.error).toContain('不支持的图片格式')
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('应该正确提取Base64图片的MIME类型', () => {
      fc.assert(
        fc.property(
          fc.constantFrom(...supportedMimeTypes),
          (mimeType) => {
            const base64 = `data:${mimeType};base64,SGVsbG8=`
            const extractedType = getMimeTypeFromBase64(base64)
            
            expect(extractedType).toBe(mimeType)
            
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('无效的Base64字符串应该返回null', () => {
      fc.assert(
        fc.property(
          fc.string().filter(s => !s.startsWith('data:')),
          (invalidString) => {
            const result = getMimeTypeFromBase64(invalidString)
            expect(result).toBeNull()
            return true
          }
        ),
        { numRuns: 100 }
      )
    })

    it('SUPPORTED_IMAGE_TYPES应该包含所有必需的格式', () => {
      const requiredTypes = ['image/png', 'image/jpeg', 'image/gif', 'image/webp']
      
      requiredTypes.forEach(type => {
        expect(SUPPORTED_IMAGE_TYPES).toContain(type)
      })
    })

    it('MAX_IMAGE_SIZE应该是10MB', () => {
      expect(MAX_IMAGE_SIZE).toBe(10 * 1024 * 1024)
    })
  })
})
