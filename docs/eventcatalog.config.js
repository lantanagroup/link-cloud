/** @type {import('@eventcatalog/core/bin/eventcatalog.config').Config} */

const baseUrl = process.env.BASE_URL ? process.env.BASE_URL : '/';

export default {
  title: 'Link EC',
  tagline: 'This internal platform provides a comprehensive view of our event-driven architecture across all systems. Use this portal to discover existing domains, explore services and their dependencies, and understand the message contracts that connect our infrastructure',
  organizationName: 'Lantana Consulting Group',
  homepageLink: 'https://lantanagroup.github.io/link-cloud',
  editUrl: 'https://github.com/lantanagroup/link-cloud/tree/dev/docs',
  trailingSlash: false,
  // Change to make the base url of the site different, by default https://{website}.com/docs,
  // changing to /company would be https://{website}.com/company/docs,
  base: baseUrl,
  // Customize the logo, add your logo to public/ folder
  logo: {
    alt: 'EventCatalog Logo',
    src: '/logo.png',
    text: 'EventCatalog'
  },
  // Enable RSS feed for your eventcatalog
  rss: {
    enabled: true,
    // number of items to include in the feed per resource (event, service, etc)
    limit: 20
  },
	chat: {
		enabled: true,
		model: 'Hermes-3-Llama-3.2-3B-q4f16_1-MLC',
		max_tokens: 4096,
		similarityResults: 50
	},
  generators: [
		[
			"@eventcatalog/generator-ai", {
				splitMarkdownFiles: true,
				includeUsersAndTeams: false,
				includeSchemas: true,
				includeCustomDocumentation: true,
				embedding: {
					provider: 'huggingface',
					model: 'Xenova/all-MiniLM-L6-v2'
				}
			}
		],
  ],
  customDocs: {
    sidebar: [
      {
        label: 'Additional Architecture',
        items: [
          { label: 'Auth Flow', slug: 'arch/auth_flow' },
          { label: 'Persistence', slug: 'arch/persistence' },
          { label: 'Retry Topics', slug: 'arch/retry_topics' },
          { label: 'Security', slug: 'arch/security' },
          { label: 'Observability & Telemetry', slug: 'arch/telemetry' }
        ]
      },
      {
        label: 'User Documentation',
        items: [
          { label: 'Tenant Management', slug: 'user/tenant_management' }
        ]
      },
      {
        label: 'Configuration',
        items: [
          { label: 'Configuring .NET Services', slug: 'config/dotnet' },
          { label: 'Configuring Java Services', slug: 'config/java' }
        ]
      },
      {
        label: 'Development',
        items: [
          { label: 'API Guidance', slug: 'dev/api_guidance' },
          { label: 'Authorization Policies', slug: 'dev/auth_policies' },
          { label: 'Logging & Error Handling', slug: 'dev/logging_error_handling' },
          { label: 'Open Telemetry', slug: 'dev/otel' },
          { label: 'Testing', slug: 'dev/testing' }
        ]
      },
      {
        label: 'Change Proposals',
        items: [
          { label: 'Submit Per Org ID (LNK-3168)', slug: 'proposals/submit-per-org-id' }
        ]
      }
    ]
  },
  // required random generated id used by eventcatalog
  cId: '19993e2d-5d40-485e-8301-a445c8325f6e'
}
