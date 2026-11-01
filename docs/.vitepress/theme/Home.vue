<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { withBase } from 'vitepress'

// ---------- Migration converter ----------

const examples = {
  'Program.cs': `using MediatR;
using MyApp.Users;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMediatR(cfg =>
{
    cfg.LicenseKey = builder.Configuration["MediatR:Key"];
    cfg.RegisterServicesFromAssemblyContaining<GetUserHandler>();
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
});

var app = builder.Build();

app.MapGet("/users/{id}", (int id, ISender sender) =>
    sender.Send(new GetUser(id)));

app.Run();`,
  'LoggingBehavior.cs': `using MediatR;
using Microsoft.Extensions.Logging;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Name}", typeof(TRequest).Name);
        return await next();
    }
}`,
  'OrderPlaced.cs': `using MediatR;
using MediatR.Pipeline;

public sealed record OrderPlaced(int OrderId) : INotification;

public sealed class SendConfirmation(IEmailSender email)
    : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced n, CancellationToken ct)
        => email.SendAsync(n.OrderId, ct);
}

public sealed class Checkout(IPublisher publisher)
{
    public Task Complete(int orderId)
        => publisher.Publish(new OrderPlaced(orderId));
}`,
} as const

type ExampleName = keyof typeof examples
const exampleNames = Object.keys(examples) as ExampleName[]

const example = ref<ExampleName>('Program.cs')
const source = ref<string>(examples['Program.cs'])
const editing = ref(false)
const rules = reactive({ namespace: true, registration: true, licenseKey: true })

// Where each MediatR namespace went; a using of any other one is left as it is.
const namespaces: Record<string, string> = {
  '': 'AnyMediator',
  '.Pipeline': 'AnyMediator',
  '.NotificationPublishers': 'AnyMediator.NotificationPublishers',
}

function pickExample(name: ExampleName) {
  example.value = name
  source.value = examples[name]
  editing.value = false
}

type Row = { kind: 'same' | 'del' | 'add'; text: string }

const converted = computed(() => {
  const lines = source.value.replace(/\r\n/g, '\n').split('\n')
  const rows: Row[] = []
  let changed = 0
  const emitted = new Set<string>()

  for (const line of lines) {
    let next: string | null = line

    if (rules.namespace) {
      const using = /^(\s*)(global\s+)?using\s+(Open)?MediatR((?:\.\w+)*)\s*;/.exec(line)
      const target = using ? namespaces[using[4]] : undefined
      if (using && target) {
        next = emitted.has(target) ? null : `${using[1]}${using[2] ?? ''}using ${target};`
        emitted.add(target)
      }
    }
    if (next !== null && rules.registration) next = next.replace(/\bAdd(Open)?MediatR\s*\(/g, 'AddAnyMediator(').replace(/\bOpenMediatRConfiguration\b/g, 'AnyMediatorConfiguration')
    if (next !== null && rules.licenseKey && /\bLicenseKey\s*=/.test(next)) next = null

    if (next === line) {
      rows.push({ kind: 'same', text: line })
    } else {
      changed++
      rows.push({ kind: 'del', text: line })
      if (next !== null) rows.push({ kind: 'add', text: next })
    }
  }

  return { rows, changed }
})

const resultText = computed(() =>
  converted.value.rows.filter(r => r.kind !== 'del').map(r => r.text).join('\n'))

const copied = ref('')
async function copy(text: string, key: string) {
  try {
    await navigator.clipboard.writeText(text)
    copied.value = key
    setTimeout(() => { if (copied.value === key) copied.value = '' }, 1600)
  } catch {
    copied.value = ''
  }
}

// ---------- Light syntax colouring for the static fragments ----------

const keywords = /\b(using|var|public|sealed|class|record|await|return|async|new|where|typeof|static|readonly|interface|int|string|Task)\b/g

function escape(s: string) {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

function colour(line: string) {
  return escape(line)
    .replace(/(&quot;|"[^"]*")/g, '<span class="t-str">$1</span>')
    .replace(keywords, '<span class="t-kw">$1</span>')
}

// ---------- API panel with margin notes ----------

const groups = [
  {
    id: 'requests', name: 'Requests and streams', link: '/guide/requests',
    note: 'One handler per request. Send returns its response; CreateStream yields items as they arrive.',
    members: ['IRequest<TResponse>', 'IRequestHandler<TRequest, TResponse>', 'IStreamRequestHandler<TRequest, T>', 'ISender.Send / CreateStream'],
  },
  {
    id: 'notifications', name: 'Notifications', link: '/guide/notifications',
    note: 'Any number of handlers per event, run one after another or in parallel.',
    members: ['INotification', 'INotificationHandler<TNotification>', 'IPublisher.Publish'],
  },
  {
    id: 'pipeline', name: 'Pipeline behaviors', link: '/guide/pipeline-behaviors',
    note: 'Middleware around every request, run in the order you register it.',
    members: ['IPipelineBehavior<TRequest, TResponse>', 'IStreamPipelineBehavior<TRequest, T>', 'RequestHandlerDelegate<TResponse>'],
  },
  {
    id: 'processors', name: 'Pre- and post-processors', link: '/guide/processors',
    note: 'Lighter hooks that run just before and just after the handler.',
    members: ['IRequestPreProcessor<TRequest>', 'IRequestPostProcessor<TRequest, TResponse>'],
  },
  {
    id: 'exceptions', name: 'Exception handling', link: '/guide/exception-handling',
    note: 'Recover with a fallback response, or log and let the exception continue.',
    members: ['IRequestExceptionHandler<TRequest, TResponse, TException>', 'IRequestExceptionAction<TRequest, TException>', 'RequestExceptionHandlerState<TResponse>'],
  },
  {
    id: 'sinks', name: 'Notification sinks', link: '/guide/notification-sinks',
    note: 'Send the same notification to in-process handlers, a broker or a webhook.',
    members: ['INotificationSink', 'INotificationPublisher', 'TaskWhenAllPublisher'],
  },
]
const focused = ref<string | null>(null)

// ---------- Entrance: parts of each section rise in once, staggered ----------

const root = ref<HTMLElement | null>(null)
let observer: IntersectionObserver | null = null

onMounted(() => {
  const el = root.value
  if (!el || !('IntersectionObserver' in window)) return
  el.dataset.js = ''
  observer = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue
      const parts = entry.target.querySelectorAll<HTMLElement>('[data-rise]')
      parts.forEach((p, i) => {
        p.style.transitionDelay = `${i * 75}ms`
        p.dataset.in = ''
        // Drop the stagger once revealed, so hover feedback is not delayed.
        setTimeout(() => { p.style.transitionDelay = '' }, 600 + i * 75)
      })
      observer?.unobserve(entry.target)
    }
  }, { rootMargin: '0px 0px -10% 0px' })
  el.querySelectorAll('[data-section]').forEach(s => observer!.observe(s))
})

onBeforeUnmount(() => observer?.disconnect())
</script>

<template>
  <div ref="root" class="om">
    <!-- Hero: two doors, one per kind of visitor -->
    <section class="om-wall" data-section>
      <h1 class="om-title" data-rise><span>The free mediator for .NET</span> <span>with the MediatR API</span></h1>
      <p class="om-sub" data-rise>
        AnyMediator runs your requests, notifications and pipeline behaviors in-process.
        Apache-2.0, with no license key and no revenue limit.
      </p>
      <div class="om-install" data-rise>
        <code>dotnet add package AnyMediator</code>
        <button class="om-button" type="button" @click="copy('dotnet add package AnyMediator', 'hero')">
          {{ copied === 'hero' ? 'Copied' : 'Copy' }}
        </button>
      </div>

      <div class="om-doors" :class="{ 'has-focus': focused?.startsWith('door') }">
        <a class="om-door" href="#migrate" data-rise
           :class="{ 'is-dim': focused === 'door-new' }"
           @mouseenter="focused = 'door-mediatr'" @mouseleave="focused = null"
           @focus="focused = 'door-mediatr'" @blur="focused = null">
          <div class="om-door-pic" data-ui aria-hidden="true">
            <div class="om-file">Program.cs</div>
            <pre class="om-mini"><code><span class="d-del">- using MediatR;</span>
<span class="d-add">+ using AnyMediator;</span>

<span class="d-del">- services.AddMediatR(cfg =></span>
<span class="d-add">+ services.AddAnyMediator(cfg =></span>
  {
<span class="d-del">-   cfg.LicenseKey = "…";</span>
    cfg.RegisterServices…
  });</code></pre>
            <div class="om-file">GetUserHandler.cs, unchanged</div>
            <pre class="om-mini"><code>  <span class="t-kw">class</span> GetUserHandler
    : IRequestHandler&lt;
        GetUser, User&gt;</code></pre>
          </div>
          <span class="om-door-name">I use MediatR</span>
          <span class="om-door-line">See the three lines that change</span>
        </a>

        <a class="om-door" href="#start" data-rise
           :class="{ 'is-dim': focused === 'door-mediatr' }"
           @mouseenter="focused = 'door-new'" @mouseleave="focused = null"
           @focus="focused = 'door-new'" @blur="focused = null">
          <div class="om-door-pic" data-ui aria-hidden="true">
            <div class="om-file">Terminal</div>
            <pre class="om-mini"><code><span class="d-prompt">$</span> dotnet add package
  AnyMediator</code></pre>
            <div class="om-file">GetUser.cs</div>
            <pre class="om-mini"><code><span class="t-kw">record</span> GetUser(<span class="t-kw">int</span> Id)
  : IRequest&lt;User&gt;;

<span class="t-kw">class</span> GetUserHandler
  : IRequestHandler&lt;
      GetUser, User&gt;</code></pre>
            <div class="om-file">Program.cs</div>
            <pre class="om-mini"><code>services.AddAnyMediator(cfg =>
  cfg.RegisterServices…);</code></pre>
          </div>
          <span class="om-door-name">I’m starting fresh</span>
          <span class="om-door-line">Install, register, send</span>
        </a>
      </div>
    </section>

    <!-- Live migration -->
    <section id="migrate" class="om-section" data-section>
      <div class="om-column">
        <h2 class="om-h2" data-rise>Try the migration</h2>
        <p class="om-text" data-rise>
          Pick a file from a MediatR project, or paste your own. The changes AnyMediator needs
          are applied as you type, in your browser.
        </p>
      </div>

      <div class="om-demo" data-ui data-rise>
        <div class="om-demo-code">
          <div class="om-demo-bar">
            <span class="om-demo-file">{{ editing ? 'Your code' : example }}</span>
            <button class="om-chip" type="button" @click="copy(resultText, 'result')">
              {{ copied === 'result' ? 'Copied' : 'Copy result' }}
            </button>
          </div>
          <textarea v-if="editing" v-model="source" class="om-demo-input" spellcheck="false"
                    aria-label="MediatR code to convert"></textarea>
          <pre v-else class="om-diff" aria-label="Converted code"><code><span
            v-for="(row, i) in converted.rows" :key="i" class="om-diff-row" :class="'r-' + row.kind"
          ><span class="om-diff-sign">{{ row.kind === 'add' ? '+' : row.kind === 'del' ? '−' : ' ' }}</span>{{ row.text }}
</span></code></pre>
        </div>

        <div class="om-demo-controls">
          <div class="om-ctl om-ctl-files" role="group" aria-label="Example file">
            <button v-for="name in exampleNames" :key="name" type="button" class="om-seg"
                    :aria-pressed="!editing && example === name" @click="pickExample(name)">{{ name }}</button>
          </div>
          <label class="om-ctl">
            <span>Namespace</span>
            <input v-model="rules.namespace" type="checkbox" class="om-switch">
          </label>
          <label class="om-ctl">
            <span>AddMediatR call</span>
            <input v-model="rules.registration" type="checkbox" class="om-switch">
          </label>
          <label class="om-ctl">
            <span>License key</span>
            <input v-model="rules.licenseKey" type="checkbox" class="om-switch">
          </label>
          <div class="om-ctl om-ctl-value">
            <span>Lines changed</span>
            <strong>{{ converted.changed }}</strong>
          </div>
          <p class="om-ctl om-ctl-notes">
            Behaviors, handlers, Send(object) and Publish work as in MediatR.
          </p>
          <button class="om-ctl om-edit" type="button" @click="editing = !editing">
            {{ editing ? 'Show the result' : 'Paste your own code' }}
          </button>
          <a class="om-demo-link" :href="withBase('/guide/migrating-from-mediatr')">Read the full migration guide</a>
        </div>
      </div>
      <p class="om-column om-after" data-rise>
        Then swap the package: <code>dotnet remove package MediatR</code> and
        <code>dotnet add package AnyMediator</code>.
      </p>
    </section>

    <!-- Call and response -->
    <section id="start" class="om-section" data-section>
      <div class="om-column">
        <h2 class="om-h2" data-rise>Starting fresh</h2>

        <div class="om-step" data-rise>
          <p class="om-step-say">First, install the package.</p>
          <div class="om-call">
            <pre><code><span class="d-prompt">$</span> dotnet add package AnyMediator</code></pre>
            <button class="om-chip" type="button" @click="copy('dotnet add package AnyMediator', 'install')">
              {{ copied === 'install' ? 'Copied' : 'Copy' }}
            </button>
          </div>
          <div class="om-response" data-ui>
            <pre><code>info : Adding PackageReference for package 'AnyMediator' into project 'MyApp.csproj'.
info : PackageReference for package 'AnyMediator' version '2.0.0' added to file 'MyApp.csproj'.</code></pre>
          </div>
        </div>

        <div class="om-step" data-rise>
          <p class="om-step-say">Then, register it with your services.</p>
          <div class="om-call">
            <pre><code><span v-for="(l, i) in [
              'builder.Services.AddAnyMediator(cfg =>',
              '{',
              '    cfg.RegisterServicesFromAssemblyContaining<Program>();',
              '    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));',
              '    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));',
              '});']" :key="i" v-html="colour(l) + '\n'"></span></code></pre>
          </div>
          <div class="om-response" data-ui>
            <table class="om-reg">
              <caption>Added to the service collection</caption>
              <tbody>
                <tr><td><code>ISender, IPublisher, IMediator</code></td><td>the mediator itself</td></tr>
                <tr><td><code>IRequestHandler&lt;GetUser, User&gt;</code></td><td>GetUserHandler, found by scanning</td></tr>
                <tr><td><code>IPipelineBehavior&lt;,&gt;</code></td><td>LoggingBehavior, then ValidationBehavior</td></tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="om-step" data-rise>
          <p class="om-step-say">Finally, send a request.</p>
          <div class="om-call">
            <pre><code><span class="t-kw">var</span> user = <span class="t-kw">await</span> sender.Send(<span class="t-kw">new</span> GetUser(42));</code></pre>
          </div>
        </div>
      </div>

      <div class="om-trace" data-ui data-rise>
        <p class="om-trace-title">What runs, in registration order</p>
        <ol class="om-trace-list">
          <li style="--depth: 0"><code>LoggingBehavior</code> <span>before next()</span></li>
          <li style="--depth: 1"><code>ValidationBehavior</code> <span>before next()</span></li>
          <li style="--depth: 2" class="is-handler"><code>GetUserHandler.Handle</code> <span>returns the user with Id 42</span></li>
          <li style="--depth: 1"><code>ValidationBehavior</code> <span>after next()</span></li>
          <li style="--depth: 0"><code>LoggingBehavior</code> <span>after next(), then the response goes back to the caller</span></li>
        </ol>
      </div>
    </section>

    <!-- API plate with margin notes -->
    <section class="om-section" data-section>
      <div class="om-plate">
        <h2 class="om-h2 om-plate-head" data-rise>What is in the box</h2>
        <template v-for="(g, i) in groups" :key="g.id">
          <a class="om-note" :class="i % 2 === 0 ? 'is-left' : 'is-right'" :style="{ gridRow: i + 2 }"
             :href="withBase(g.link)" data-rise
             @mouseenter="focused = g.id" @mouseleave="focused = null"
             @focus="focused = g.id" @blur="focused = null">
            <span class="om-note-name">{{ g.name }}</span>
            <span class="om-note-text">{{ g.note }}</span>
          </a>
          <div class="om-plate-group" :class="{ 'is-dim': focused && focused !== g.id && !focused.startsWith('door'), 'is-first': i === 0, 'is-last': i === groups.length - 1 }"
               :style="{ gridRow: i + 2 }" data-ui>
            <span class="om-plate-ns">{{ g.name }}</span>
            <code v-for="m in g.members" :key="m">{{ m }}</code>
          </div>
        </template>
      </div>
    </section>

    <!-- Where it runs -->
    <section class="om-section om-strip" data-section>
      <p class="om-strip-row" data-rise>
        <span class="om-strip-intro">Runs on</span>
        <span>.NET 8</span>
        <span>.NET 10</span>
        <span>ASP.NET Core</span>
        <span>Worker services</span>
        <span>Azure Functions</span>
        <span>Console apps</span>
      </p>
    </section>

    <!-- Close -->
    <section class="om-section om-close" data-section>
      <div class="om-column">
        <p class="om-close-line" data-rise>Install it, register it, and keep the handlers you already wrote.</p>
        <div class="om-actions" data-rise>
          <a class="om-button" :href="withBase('/guide/getting-started')">Read the guide</a>
          <a class="om-link" href="https://github.com/Adiakys/AnyMediator">View the source on GitHub</a>
        </div>
      </div>
    </section>

    <footer class="om-foot">
      <span class="om-foot-what">In-process mediator for .NET. Not affiliated with MediatR.</span>
      <nav class="om-foot-links" aria-label="Footer">
        <a :href="withBase('/guide/getting-started')">Guide</a>
        <a :href="withBase('/guide/migrating-from-mediatr')">Migration</a>
        <a href="https://www.nuget.org/packages/AnyMediator">NuGet</a>
        <a href="https://github.com/Adiakys/AnyMediator">GitHub</a>
        <a :href="withBase('/llms.txt')">llms.txt</a>
      </nav>
      <span class="om-foot-legal">© 2026 Andrea Ficarra. Apache-2.0</span>
    </footer>
  </div>
</template>

<style scoped>
.om {
  --gutter: clamp(16px, 4vw, 32px);
  --column: 640px;
  --frame: 1040px;
  background: var(--om-paper);
  color: var(--om-ink);
  font-size: var(--om-step-body);
  line-height: 1.5;
  letter-spacing: -0.01em;
}

.om a { color: inherit; text-decoration: none; }
.om code, .om pre { font-family: var(--vp-font-family-mono); }

/* ---------- Hero ---------- */
.om-wall {
  background: var(--om-navy);
  color: var(--om-wall-ink);
  padding: 88px var(--gutter) 150px;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
}
.om-title span { display: block; }
.om-title {
  margin: 0;
  font-size: var(--om-step-display);
  line-height: 1.05;
  font-weight: 500;
  letter-spacing: -0.03em;
}
.om-sub {
  margin: 32px 0 0;
  max-width: 46ch;
  color: var(--om-wall-muted);
  font-size: var(--om-step-body);
}
.om-doors {
  margin-top: 72px;
  display: flex;
  gap: 80px;
  justify-content: center;
}
.om-door {
  width: 320px;
  display: flex;
  flex-direction: column;
  text-align: left;
  transition: opacity 180ms var(--om-ease);
}
.om-door.is-dim { opacity: 0.5; }
.om-door-pic {
  aspect-ratio: 4 / 5;
  background: var(--om-code-bg);
  color: var(--om-code-ink);
  padding: 20px 18px;
  overflow: hidden;
  transition: transform 180ms var(--om-ease);
}
.om-door:hover .om-door-pic,
.om-door:focus-visible .om-door-pic { transform: translateY(-4px); }
.om-door:focus-visible { outline: 2px solid var(--om-cyan); outline-offset: 6px; }
.om-file {
  font-size: 14px;
  color: var(--om-code-muted);
  font-family: var(--vp-font-family-base);
  margin-bottom: 10px;
}
.om-file + .om-mini { margin-bottom: 22px; }
.om-mini {
  margin: 0;
  font-size: 14px;
  line-height: 1.6;
  white-space: pre;
}
.om-door-name { margin-top: 18px; font-size: 21px; font-weight: 500; }
.om-door-line { margin-top: 4px; font-size: 17px; color: var(--om-wall-muted); }

.d-del { color: #ffb4b4; }
.d-add { color: #9be7b8; }
.d-prompt { color: var(--om-cyan); }
.t-kw, :deep(.t-kw) { color: #7cc8ec; }
:deep(.t-str) { color: #f2c98b; }

.om-install {
  margin-top: 32px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 0 0 18px;
  background: var(--om-code-bg);
  border-radius: var(--om-radius);
}
.om-install code { font-size: 14px; color: var(--om-code-ink); }
.om-install .om-button { margin: 4px; }

/* ---------- Shared section rhythm ---------- */
.om-section {
  padding: clamp(96px, 12vw, 160px) var(--gutter) 0;
}
.om-column {
  max-width: var(--column);
  margin-left: max(0px, calc((100% - var(--frame)) / 2));
}
.om-h2 {
  margin: 0;
  font-size: var(--om-step-head);
  line-height: 1.1;
  font-weight: 500;
  letter-spacing: -0.03em;
}
.om-text {
  margin: 20px 0 0;
  font-size: var(--om-step-body);
}
.om-after {
  margin-top: 28px;
  color: var(--om-muted);
}
.om-after code, .om-text code {
  font-size: 0.875em;
  background: var(--om-surface);
  padding: 2px 6px;
}

/* ---------- Live demo ---------- */
.om-demo {
  max-width: var(--frame);
  margin: 48px auto 0;
  display: grid;
  grid-template-columns: 62fr 38fr;
  min-height: 600px;
  background: var(--om-surface);
  border-radius: var(--om-radius);
  overflow: hidden;
}
.om-demo-code {
  display: flex;
  flex-direction: column;
  background: var(--om-code-bg);
  color: var(--om-code-ink);
  min-width: 0;
}
.om-demo-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 18px;
  background: #16264a;
  font-size: 14px;
}
.om-demo-file { color: var(--om-code-muted); font-family: var(--vp-font-family-base); }
.om-diff, .om-demo-input {
  flex: 1;
  margin: 0;
  padding: 18px 0;
  font-size: 14px;
  line-height: 1.65;
  overflow: auto;
}
.om-demo-input {
  width: 100%;
  min-height: 520px;
  padding: 18px;
  background: transparent;
  color: inherit;
  border: 0;
  resize: vertical;
  font-family: var(--vp-font-family-mono); /* taste-skill-ignore: code editor */
}
.om-demo-input:focus-visible { outline: 2px solid var(--om-cyan); outline-offset: -2px; }
.om-diff-row { display: block; padding: 0 18px 0 0; white-space: pre; }
.om-diff-sign { display: inline-block; width: 30px; text-align: center; color: var(--om-code-muted); user-select: none; }
.r-del { background: rgba(255, 120, 120, 0.14); color: #ffc9c9; }
.r-add { background: rgba(110, 220, 160, 0.14); color: #b9f0cf; }
.r-del .om-diff-sign { color: #ff9b9b; }
.r-add .om-diff-sign { color: #7fe0a8; }

.om-demo-controls {
  display: flex;
  flex-direction: column;
  padding: 12px 20px 20px;
  font-size: 17px;
}
.om-ctl {
  min-height: 48px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  cursor: pointer;
}
.om-ctl-files { flex-wrap: wrap; justify-content: flex-start; gap: 6px; padding: 8px 0 12px; cursor: default; }
.om-seg {
  font: inherit;
  font-size: 14px;
  padding: 7px 12px;
  border-radius: var(--om-radius);
  background: var(--om-surface-2);
  color: var(--om-ink);
  cursor: pointer;
  transition: background 140ms var(--om-ease), transform 140ms var(--om-ease);
}
.om-seg:active { transform: scale(0.97); }
.om-seg[aria-pressed='true'] { background: var(--om-navy); color: #fff; }
.om-switch {
  appearance: none;
  width: 40px;
  height: 22px;
  border-radius: 11px;
  background: var(--om-surface-2);
  position: relative;
  cursor: pointer;
  flex: none;
  transition: background 140ms var(--om-ease);
}
.om-switch::after {
  content: '';
  position: absolute;
  top: 3px;
  left: 3px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: #fff;
  transition: transform 160ms var(--om-ease);
}
.om-switch:checked { background: var(--om-navy); }
.om-switch:checked::after { transform: translateX(18px); }
.om-switch:focus-visible, .om-seg:focus-visible, .om-edit:focus-visible, .om-chip:focus-visible {
  outline: 2px solid var(--om-cyan);
  outline-offset: 2px;
}
.om-ctl-value { cursor: default; }
.om-ctl-value strong { font-size: var(--om-step-lead); font-weight: 500; font-variant-numeric: tabular-nums; }
.om-ctl-notes {
  margin: 0;
  flex-direction: column;
  align-items: flex-start;
  justify-content: center;
  gap: 4px;
  padding: 10px 0;
  color: var(--om-muted);
  cursor: default;
}
.om-edit {
  margin-top: 8px;
  justify-content: center;
  font: inherit;
  font-size: 17px;
  background: var(--om-cyan);
  color: #0f1b33;
  border-radius: var(--om-radius);
  transition: transform 140ms var(--om-ease), background 140ms var(--om-ease);
}
.om-edit:hover { background: #4dbbe9; }
.om-edit:active { transform: scale(0.98); }
.om-demo-link {
  margin-top: auto;
  padding-top: 20px;
  font-weight: 500;
  text-decoration: underline !important;
  text-underline-offset: 3px;
}
.om-chip {
  font: inherit;
  font-size: 14px;
  padding: 4px 10px;
  border-radius: var(--om-radius);
  background: rgba(255, 255, 255, 0.1);
  color: var(--om-code-ink);
  cursor: pointer;
  transition: background 140ms var(--om-ease);
}
.om-chip:hover { background: rgba(255, 255, 255, 0.18); }

/* ---------- Call and response: the call on the left, what comes back on the right ---------- */
#start .om-column { max-width: var(--frame); margin-inline: auto; }
#start .om-step {
  display: grid;
  grid-template-columns: minmax(0, 7fr) minmax(0, 5fr);
  column-gap: 32px;
  align-items: start;
}
#start .om-step-say { grid-column: 1 / -1; }
#start .om-response { margin: 16px 0 0; }
@media (max-width: 860px) {
  #start .om-step { grid-template-columns: minmax(0, 1fr); }
  #start .om-response { margin-left: 0; }
}

.om-step { margin-top: 72px; }
.om-step:first-of-type { margin-top: 56px; }
.om-step-say { margin: 0; font-size: 21px; font-weight: 500; }
.om-call {
  position: relative;
  margin-top: 16px;
  background: var(--om-code-bg);
  color: var(--om-code-ink);
  border-radius: var(--om-radius);
}
.om-call pre { margin: 0; padding: 20px; font-size: 14px; line-height: 1.65; overflow-x: auto; }
.om-call .om-chip { position: absolute; top: 14px; right: 14px; }
.om-response {
  margin: 16px 0 0 56px;
  background: var(--om-surface);
  border-radius: var(--om-radius);
  padding: 16px 20px;
  font-size: 17px;
}
.om-response pre { margin: 0; font-size: 14px; line-height: 1.6; white-space: pre-wrap; color: var(--om-muted); }
.om-reg { width: 100%; border-collapse: collapse; }
.om-reg caption { text-align: left; color: var(--om-muted); padding-bottom: 8px; }
.om-reg td { padding: 8px 0; vertical-align: top; border-top: 1px solid var(--om-surface-2); }
.om-reg td:first-child { padding-right: 16px; }
.om-reg code { font-size: 14px; }
.om-reg td:last-child { color: var(--om-muted); }

.om-trace {
  max-width: var(--frame);
  margin: 24px auto 0;
  padding: 28px 32px;
  background: var(--om-surface);
  border-radius: var(--om-radius);
}
.om-trace-title { margin: 0 0 16px; font-weight: 500; }
.om-trace-list { list-style: none; margin: 0; padding: 0; }
.om-trace-list code { font-size: 14px; }
.om-trace-list li {
  padding: 10px 14px;
  margin-left: calc(var(--depth) * 40px);
  background: var(--om-paper);
}
.om-trace-list li + li { margin-top: 4px; }
.om-trace-list li span { font-family: var(--vp-font-family-base); color: var(--om-muted); margin-left: 10px; font-size: 14px; }
.om-trace-list .is-handler { background: var(--om-navy); color: #fff; }
.om-trace-list .is-handler span { color: var(--om-wall-muted); }

/* ---------- API plate ---------- */
.om-plate {
  max-width: var(--frame);
  margin: 0 auto;
  display: grid;
  grid-template-columns: 1fr 400px 1fr;
  column-gap: 48px;
}
.om-plate-head { grid-column: 1 / -1; grid-row: 1; margin-bottom: 40px; }
.om-plate-group {
  grid-column: 2;
  background: var(--om-code-bg);
  color: var(--om-code-ink);
  padding: 22px 24px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  transition: opacity 180ms var(--om-ease);
}
.om-plate-group.is-first { border-radius: var(--om-radius) var(--om-radius) 0 0; }
.om-plate-group.is-last { border-radius: 0 0 var(--om-radius) var(--om-radius); }
.om-plate-group.is-dim { opacity: 0.3; }
.om-plate-ns { font-size: 14px; color: var(--om-code-muted); margin-bottom: 4px; }
.om-plate-group code { font-size: 14px; line-height: 1.5; overflow-wrap: anywhere; }
.om-note {
  align-self: center;
  max-width: 28ch;
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 8px 0;
}
.om-note.is-left { grid-column: 1; justify-self: end; text-align: right; }
.om-note.is-right { grid-column: 3; justify-self: start; }
.om-note-name {
  font-size: 17px;
  font-weight: 500;
  text-decoration: underline;
  text-decoration-color: transparent;
  text-underline-offset: 3px;
  transition: text-decoration-color 180ms var(--om-ease);
}
.om-note:hover .om-note-name, .om-note:focus-visible .om-note-name { text-decoration-color: currentColor; }
.om-note:focus-visible { outline: 2px solid var(--om-cyan); outline-offset: 4px; }
.om-note-text { font-size: 17px; color: var(--om-muted); }

/* ---------- Strip ---------- */
.om-strip-row {
  max-width: var(--frame);
  margin: 0 auto;
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  flex-wrap: wrap;
  gap: 16px 32px;
  font-size: 21px;
}
.om-strip-intro { font-size: 17px; color: var(--om-muted); }

/* ---------- Close and footer ---------- */
.om-close { padding-top: 26svh; padding-bottom: 20svh; }
.om-close-line { margin: 0; font-size: 21px; max-width: 34ch; }
.om-actions { margin-top: 24px; display: flex; align-items: center; gap: 24px; flex-wrap: wrap; }
.om-button {
  display: inline-flex;
  align-items: center;
  height: 44px;
  padding: 0 20px;
  background: var(--om-cyan);
  color: #0f1b33 !important;
  font: inherit;
  font-weight: 500;
  border: 0;
  cursor: pointer;
  border-radius: var(--om-radius);
  transition: transform 140ms var(--om-ease), background 140ms var(--om-ease);
}
.om-button:hover { background: #4dbbe9; }
.om-button:active { transform: scale(0.97); }
.om-button:focus-visible, .om-link:focus-visible, .om-foot a:focus-visible { outline: 2px solid var(--om-navy); outline-offset: 3px; }
.om-link { text-decoration: underline !important; text-underline-offset: 3px; transition: color 180ms var(--om-ease); }
.om-link:hover { color: var(--om-navy); }

.om-foot {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  align-items: baseline;
  gap: 16px;
  padding: 0 var(--gutter) 30px;
  font-size: 14px;
  color: var(--om-muted);
}
.om-foot-links { display: flex; gap: 24px; flex-wrap: wrap; justify-content: center; }
.om-foot-links a { color: var(--om-ink); transition: color 180ms var(--om-ease); }
.om-foot-links a:hover { color: var(--om-navy); }
.om-foot-legal { justify-self: end; }

/* ---------- Entrance (only once the script runs) ---------- */
.om[data-js] [data-rise] {
  opacity: 0;
  transform: translateY(20px);
  transition: opacity 520ms var(--om-ease), transform 520ms var(--om-ease);
}
.om[data-js] [data-rise][data-in] { opacity: 1; transform: none; }
.om[data-js] .om-door[data-rise][data-in].is-dim { opacity: 0.5; }

@media (prefers-reduced-motion: reduce) {
  .om[data-js] [data-rise] { transform: none; transition: opacity 200ms linear; }
  .om-door-pic, .om-button, .om-seg, .om-edit { transition: none; }
}

/* ---------- Dark mode ---------- */
.dark .om-wall { background: #102a5c; }
.dark .om-button:focus-visible, .dark .om-link:focus-visible, .dark .om-foot a:focus-visible { outline-color: var(--om-cyan); }
.dark .om-link:hover, .dark .om-foot-links a:hover { color: var(--om-cyan); }
.dark .om-trace-list .is-handler { background: #24509e; }
.dark .om-plate-group, .dark .om-call, .dark .om-demo-code { background: #17284b; }
.dark .om-demo-bar { background: #1d3159; }

/* ---------- Phone ---------- */
@media (max-width: 860px) {
  .om-demo { grid-template-columns: 1fr; min-height: 0; }
  .om-diff { max-height: 420px; }
  .om-demo-input { min-height: 360px; }
  .om-ctl { min-height: 52px; }
  .om-plate { grid-template-columns: 1fr; }
  .om-plate-head { margin-bottom: 24px; }
  .om-plate-group, .om-note { grid-row: auto !important; grid-column: 1 !important; }
  .om-plate-group { border-radius: 0 !important; }
  .om-note { justify-self: start !important; text-align: left !important; max-width: none; padding: 16px 0 40px; }
  .om-plate { display: flex; flex-direction: column; }
  .om-plate > .om-plate-head { order: 0; }
  .om-foot { grid-template-columns: 1fr; gap: 8px; padding-top: 0; }
  .om-foot-links { justify-content: flex-start; }
  .om-foot-legal { justify-self: start; }
  .om-strip-row { justify-content: flex-start; display: grid; grid-template-columns: 1fr 1fr; font-size: 17px; }
  .om-strip-intro { grid-column: 1 / -1; font-size: 17px; }
}

@media (max-width: 640px) {
  .om-wall { padding: 64px 20px 96px; }
  .om-title { font-size: 6.6vw; }
  .om-doors { flex-direction: column; gap: 40px; margin-top: 48px; width: 100%; max-width: 360px; }
  .om-door { width: 100%; }
  .om-door-pic { aspect-ratio: auto; }
  .om-door-name { font-size: 17px; }
  .om-door-line { font-size: 14px; }
  .om-response { margin-left: 0; }
  .om-trace { padding: 20px 16px; }
  .om-trace-list li { margin-left: calc(var(--depth) * 16px); }
  .om-trace-list li span { display: block; margin: 2px 0 0; }
  .om-close-line { font-size: 17px; }
}
</style>
