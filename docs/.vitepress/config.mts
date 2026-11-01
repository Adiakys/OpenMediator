import { defineConfig } from 'vitepress'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

const siteUrl = 'https://adiakys.github.io/AnyMediator/'
const description =
  'AnyMediator is a free, open-source (Apache-2.0) MediatR alternative for .NET with a MediatR-compatible API: requests, notifications, streams, pipeline behaviors, processors and exception handlers.'

// Order matters: it is the sidebar order and the order of llms-full.txt.
const guide = [
  { text: 'Getting started', link: '/guide/getting-started' },
  { text: 'Migrating from MediatR', link: '/guide/migrating-from-mediatr' },
  { text: 'Requests', link: '/guide/requests' },
  { text: 'Stream requests', link: '/guide/stream-requests' },
  { text: 'Notifications', link: '/guide/notifications' },
  { text: 'Pipeline behaviors', link: '/guide/pipeline-behaviors' },
  { text: 'Pre- and post-processors', link: '/guide/processors' },
  { text: 'Exception handling', link: '/guide/exception-handling' },
  { text: 'Notification sinks', link: '/guide/notification-sinks' },
  { text: 'Configuration', link: '/guide/configuration' },
  { text: 'Tracing and metrics', link: '/guide/tracing' },
  { text: 'Performance', link: '/guide/performance' },
]

export default defineConfig({
  lang: 'en-US',
  title: 'AnyMediator',
  titleTemplate: ':title | AnyMediator, the free MediatR alternative for .NET',
  description,
  base: '/AnyMediator/',
  cleanUrls: true,
  lastUpdated: true,
  sitemap: { hostname: siteUrl },

  head: [
    ['link', { rel: 'icon', type: 'image/png', href: '/AnyMediator/icon.png' }],
    ['link', { rel: 'preconnect', href: 'https://api.fontshare.com' }],
    ['link', { rel: 'preconnect', href: 'https://cdn.fontshare.com', crossorigin: '' }],
    ['link', { rel: 'stylesheet', href: 'https://api.fontshare.com/v2/css?f[]=switzer@400,500&display=swap' }],
    ['meta', { name: 'keywords', content: 'MediatR alternative, free MediatR, mediator, .NET, C#, CQRS, pipeline behavior, Apache-2.0' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:site_name', content: 'AnyMediator' }],
    ['meta', { property: 'og:image', content: `${siteUrl}icon.png` }],
    ['meta', { name: 'twitter:card', content: 'summary' }],
    ['script', { type: 'application/ld+json' }, JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'SoftwareSourceCode',
      name: 'AnyMediator',
      description,
      codeRepository: 'https://github.com/Adiakys/AnyMediator',
      programmingLanguage: 'C#',
      runtimePlatform: '.NET',
      license: 'https://www.apache.org/licenses/LICENSE-2.0',
      author: { '@type': 'Person', name: 'Andrea Ficarra' },
      url: siteUrl,
    })],
  ],

  // Per-page canonical URL and og:title/description, so search engines and AI crawlers cite the right page.
  transformPageData(pageData) {
    const path = pageData.relativePath.replace(/(^|\/)index\.md$/, '$1').replace(/\.md$/, '')
    const url = siteUrl + path
    const title = pageData.frontmatter.title ?? pageData.title ?? 'AnyMediator'
    const desc = pageData.frontmatter.description ?? description
    pageData.frontmatter.head ??= []
    pageData.frontmatter.head.push(
      ['link', { rel: 'canonical', href: url }],
      ['meta', { property: 'og:url', content: url }],
      ['meta', { property: 'og:title', content: title }],
      ['meta', { property: 'og:description', content: desc }],
    )
  },

  // llms-full.txt: every guide page as one Markdown file, for LLMs and coding agents.
  buildEnd({ outDir, srcDir }) {
    const parts = guide.map(({ text, link }) => {
      const body = readFileSync(join(srcDir, `${link}.md`), 'utf8').replace(/^---[\s\S]*?---\n/, '')
      return `<!-- ${siteUrl}${link.slice(1)} -->\n${body.trim()}`
    })
    const header = `# AnyMediator\n\n> ${description}\n\nPackage: https://www.nuget.org/packages/AnyMediator\nSource: https://github.com/Adiakys/AnyMediator\n`
    writeFileSync(join(outDir, 'llms-full.txt'), [header, ...parts].join('\n\n'))
  },

  markdown: {
    theme: { light: 'github-light', dark: 'github-dark' },
  },

  themeConfig: {
    logo: '/icon.png',
    nav: [
      { text: 'Guide', link: '/guide/getting-started', activeMatch: '/guide/' },
      { text: 'Migrate from MediatR', link: '/guide/migrating-from-mediatr' },
      { text: 'NuGet', link: 'https://www.nuget.org/packages/AnyMediator' },
    ],
    sidebar: { '/guide/': [{ text: 'Guide', items: guide }] },
    socialLinks: [{ icon: 'github', link: 'https://github.com/Adiakys/AnyMediator' }],
    search: { provider: 'local' },
    editLink: {
      pattern: 'https://github.com/Adiakys/AnyMediator/edit/main/docs/:path',
      text: 'Edit this page on GitHub',
    },
    outline: { level: [2, 3] },
  },
})
