const __vite__mapDeps=(i,m=__vite__mapDeps,d=(m.f||(m.f=["_astro/svg-pan-zoom.CoVnVtc9.js","_astro/_commonjsHelpers.CE1G-McA.js","_astro/mermaid.core.D34KpiCp.js","_astro/preload-helper.DlBrccUy.js","_astro/transform.BZBRn45F.js","_astro/select.BuUfeguX.js","_astro/linear.CXDEMIoZ.js","_astro/mermaid-layout-elk.core.T7o5rYyW.js"])))=>i.map(i=>d[i]);
import{_ as x}from"./preload-helper.DlBrccUy.js";const T=new Map,M=new Map,E=new Map;let f=!1;function O(){f=!0,T.forEach(e=>{try{e.destroy()}catch{}}),T.clear(),M.forEach(e=>{e.disconnect()}),M.clear(),E.forEach(e=>{document.removeEventListener("fullscreenchange",e)}),E.clear()}function g(e,t){const n=getComputedStyle(document.documentElement).getPropertyValue(e).trim();return n?`rgb(${n.split(" ").join(", ")})`:t}function y(){const e=document.documentElement.getAttribute("data-theme")==="dark";return{isDark:e,bgColor:g("--ec-card-bg",e?"#161b22":"#ffffff"),borderColor:g("--ec-page-border",e?"#30363d":"#e2e8f0"),iconColor:g("--ec-icon-color",e?"#8b949e":"#64748b"),iconHoverColor:g("--ec-icon-hover",e?"#f0f6fc":"#0f172a"),hoverBgColor:g("--ec-content-hover",e?"#21262d":"#f1f5f9"),overlayBg:g("--ec-page-bg",e?"#0d1117":"#ffffff")}}function k(e,t,n,o,i={}){const{isLast:p=!1,isRound:l=!1}=i,r=document.createElement("button");r.type="button",r.title=t,r.innerHTML=e,r.onclick=n,r.style.cssText=`
    all: unset;
    box-sizing: border-box;
    display: flex;
    justify-content: center;
    align-items: center;
    width: 26px;
    height: 26px;
    min-width: 26px;
    min-height: 26px;
    padding: 0;
    margin: 0;
    border: none;
    background: ${o.bgColor};
    color: ${o.iconColor};
    cursor: pointer;
    transition: background-color 0.15s, color 0.15s;
    line-height: 1;
    font-size: 12px;
    ${!p&&!l?`border-bottom: 1px solid ${o.borderColor};`:""}
    ${l?"border-radius: 6px;":""}
  `;const s=r.querySelector("svg");return s&&(s.style.cssText="display: block; width: 12px; height: 12px;"),r.onmouseenter=()=>{r.style.backgroundColor=o.hoverBgColor,r.style.color=o.iconHoverColor},r.onmouseleave=()=>{r.style.backgroundColor=o.bgColor,r.style.color=o.iconColor},r}const m={plus:'<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>',minus:'<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"></line></svg>',fit:'<svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"></path></svg>',presentation:'<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M3.75 3v11.25A2.25 2.25 0 006 16.5h2.25M3.75 3h-1.5m1.5 0h16.5m0 0h1.5m-1.5 0v11.25A2.25 2.25 0 0118 16.5h-2.25m-7.5 0h7.5m-7.5 0l-1 3m8.5-3l1 3m0 0l.5 1.5m-.5-1.5h-9.5m0 0l-.5 1.5m.75-9l3-3 2.148 2.148A12.061 12.061 0 0116.5 7.605"></path></svg>',copy:'<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8.25 7.5V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M15.75 18H18a2.25 2.25 0 002.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 00-1.123-.08M15.75 18.75v-1.875a3.375 3.375 0 00-3.375-3.375h-1.5a1.125 1.125 0 01-1.125-1.125v-1.5A3.375 3.375 0 006.375 7.5H5.25m11.9-3.664A2.251 2.251 0 0015 2.25h-1.5a2.251 2.251 0 00-2.15 1.586m5.8 0c.065.21.1.433.1.664v.75h-6V4.5c0-.231.035-.454.1-.664M6.75 7.5H4.875c-.621 0-1.125.504-1.125 1.125v12c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V16.5a9 9 0 00-9-9z"></path></svg>',check:'<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>'};function S(e,t,n){const o=y(),i=document.createElement("div");return i.style.cssText=`
    position: absolute;
    bottom: 12px;
    left: 12px;
    display: flex;
    flex-direction: column;
    background: ${o.bgColor};
    border-radius: 6px;
    box-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
    border: 1px solid ${o.borderColor};
    overflow: hidden;
    z-index: 10;
  `,i.appendChild(k(m.plus,"Zoom in",e,o)),i.appendChild(k(m.minus,"Zoom out",t,o)),i.appendChild(k(m.fit,"Fit view",n,o,{isLast:!0})),i}function A(e,t,n,o,i="bottom"){const p=document.createElement("div");p.style.cssText="position: relative;";const l=document.createElement("button");l.type="button",l.innerHTML=e,l.style.cssText=`
    all: unset;
    box-sizing: border-box;
    display: flex;
    justify-content: center;
    align-items: center;
    width: 40px;
    height: 40px;
    min-width: 40px;
    min-height: 40px;
    padding: 0;
    margin: 0;
    border: none;
    border-radius: 6px;
    background: ${o.bgColor};
    color: ${o.iconColor};
    cursor: pointer;
    transition: background-color 0.15s, color 0.15s;
    box-shadow: 0 1px 3px 0 rgb(0 0 0 / 0.1), 0 1px 2px -1px rgb(0 0 0 / 0.1);
  `;const r=l.querySelector("svg");r&&(r.style.cssText="display: block; width: 20px; height: 20px;"),l.onmouseenter=()=>{l.style.backgroundColor=o.hoverBgColor,l.style.color=o.iconHoverColor},l.onmouseleave=()=>{l.style.backgroundColor=o.bgColor,l.style.color=o.iconColor},l.onclick=n;const s=document.createElement("div");s.textContent=t;let c=`
    position: absolute;
    padding: 4px 8px;
    background: #1f2937;
    color: white;
    font-size: 12px;
    border-radius: 4px;
    box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
    white-space: nowrap;
    pointer-events: none;
    opacity: 0;
    transition: opacity 0.15s;
    z-index: 50;
  `;return i==="right"?c+=`
      top: 50%;
      left: 100%;
      transform: translateY(-50%);
      margin-left: 8px;
    `:i==="left"?c+=`
      top: 50%;
      right: 100%;
      transform: translateY(-50%);
      margin-right: 8px;
    `:c+=`
      top: 100%;
      left: 50%;
      transform: translateX(-50%);
      margin-top: 8px;
    `,s.style.cssText=c,p.onmouseenter=()=>{s.style.opacity="1"},p.onmouseleave=()=>{s.style.opacity="0"},p.appendChild(l),p.appendChild(s),{wrapper:p,btn:l,tooltip:s}}function $(e){const t=y(),{wrapper:n}=A(m.presentation,"Presentation Mode",e,t,"right");return n.style.cssText=`
    position: absolute;
    top: 12px;
    left: 12px;
    z-index: 10;
  `,n}function P(e){const t=y(),n=A(m.copy,"Copy diagram code",()=>{e(),n.btn.innerHTML=m.check,n.btn.style.color="#10b981",n.tooltip.textContent="Copied!";const o=n.btn.querySelector("svg");o&&(o.style.cssText="display: block; width: 20px; height: 20px;"),setTimeout(()=>{n.btn.innerHTML=m.copy,n.btn.style.color=t.iconColor,n.tooltip.textContent="Copy diagram code";const i=n.btn.querySelector("svg");i&&(i.style.cssText="display: block; width: 20px; height: 20px;")},2e3)},t,"left");return n.wrapper.style.cssText=`
    position: absolute;
    top: 12px;
    right: 12px;
    z-index: 10;
  `,n.wrapper}function Z(e){document.fullscreenElement?document.exitFullscreen():e.requestFullscreen().catch(t=>{console.warn(`Error entering fullscreen: ${t.message}`)})}function B(){const e=document.createElement("div");return e.className="mermaid-zoom-container",e.style.cssText=`
    position: relative;
    width: 100%;
    min-height: 200px;
    overflow: hidden;
    margin: 0;
    cursor: grab;
  `,e}async function H(e,t,n,o={}){const{minZoom:i=.5,maxZoom:p=10,zoomScaleSensitivity:l=.15,maxHeight:r=500,minHeight:s=200,diagramContent:c}=o,{default:u}=await x(async()=>{const{default:a}=await import("./svg-pan-zoom.CoVnVtc9.js").then(b=>b.s);return{default:a}},__vite__mapDeps([0,1]));let d=0,h=0;const v=e.getAttribute("viewBox");if(v){const a=v.split(/[\s,]+/).map(Number);d=a[2],h=a[3]}if(d<=0||h<=0)try{const a=e.getBBox();d=a.width,h=a.height,d>0&&h>0&&!v&&e.setAttribute("viewBox",`${a.x} ${a.y} ${d} ${h}`)}catch{}if((d<=0||h<=0)&&(d=e.clientWidth||parseFloat(e.getAttribute("width")||"0")||800,h=e.clientHeight||parseFloat(e.getAttribute("height")||"0")||400),d>0&&h>0){const a=t.clientWidth||800,b=h/d,w=Math.min(Math.max(a*b,s),r);t.style.height=`${w}px`}else t.style.height=`${s}px`;e.style.width="100%",e.style.height="100%",e.removeAttribute("height"),e.removeAttribute("width");try{const a=u(e,{zoomEnabled:!0,controlIconsEnabled:!1,fit:!0,center:!0,minZoom:i,maxZoom:p,zoomScaleSensitivity:l,dblClickZoomEnabled:!0,mouseWheelZoomEnabled:!1,preventMouseEventsDefault:!0,panEnabled:!0});T.set(n,a),t.addEventListener("mousedown",()=>{t.style.cursor="grabbing"}),t.addEventListener("mouseup",()=>{t.style.cursor="grab"}),t.addEventListener("mouseleave",()=>{t.style.cursor="grab"});const b=S(()=>a.zoomIn(),()=>a.zoomOut(),()=>{a.fit(),a.center()});t.appendChild(b);const w=$(()=>Z(t));if(t.appendChild(w),c){const C=P(()=>{navigator.clipboard.writeText(c).catch(z=>{console.warn("Failed to copy diagram code:",z)})});t.appendChild(C)}const L=()=>{document.fullscreenElement===t?(a.enableMouseWheelZoom(),t.style.background=y().overlayBg):(a.disableMouseWheelZoom(),t.style.background=""),setTimeout(()=>{if(t.clientWidth>0&&t.clientHeight>0)try{a.resize(),a.fit(),a.center()}catch{}},100)};document.addEventListener("fullscreenchange",L),E.set(n,L);const _=new ResizeObserver(()=>{if(t.clientWidth>0&&t.clientHeight>0)try{a.resize(),a.fit(),a.center()}catch{}});_.observe(t),M.set(n,_)}catch(a){console.warn("Failed to initialize zoom on mermaid diagram:",a)}}async function W(e,t){if(e.length===0)return;f=!1;const{default:n}=await x(async()=>{const{default:r}=await import("./mermaid.core.D34KpiCp.js").then(s=>s.bp);return{default:r}},__vite__mapDeps([2,3,1,4,5,6]));if(t){const{icons:r}=await x(async()=>{const{icons:u}=await import("./index.BE8U3Lgf.js");return{icons:u}},[]),{iconPacks:s=[],enableSupportForElkLayout:c=!1}=t;if(s.length>0){const u=s.map(d=>({name:d,icons:r}));n.registerIconPacks(u)}if(c){const{default:u}=await x(async()=>{const{default:d}=await import("./mermaid-layout-elk.core.T7o5rYyW.js").then(h=>h.m);return{default:d}},__vite__mapDeps([7,3]));n.registerLayoutLoaders(u)}}const o=document.documentElement.getAttribute("data-theme")==="dark",i=o?"dark":"default",p={signalColor:"#f0f6fc",signalTextColor:"#f0f6fc",actorTextColor:"#0d1117",actorBkg:"#f0f6fc",actorBorder:"#484f58",actorLineColor:"#6b7280",primaryTextColor:"#f0f6fc",secondaryTextColor:"#c9d1d9",tertiaryTextColor:"#f0f6fc",lineColor:"#6b7280"};n.initialize({maxTextSize:t?.maxTextSize||1e5,flowchart:{curve:"linear",rankSpacing:0,nodeSpacing:0},startOnLoad:!1,fontFamily:"var(--sans-font)",theme:i,themeVariables:o?p:void 0,architecture:{useMaxWidth:!0}});const l=Array.from(e);for(const r of l){if(f)return;const s=r.getAttribute("data-content");if(!s)continue;const c="mermaid-"+Math.round(Math.random()*1e5);try{const u=await n.render(c,s);if(f)return;const d=B();d.innerHTML=u.svg,r.innerHTML="",r.appendChild(d);const h=d.querySelector("svg");h&&await H(h,d,c,{diagramContent:s})}catch(u){console.error("Mermaid render error:",u)}}}function F(e){const t="0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";let n="";const o=e.length;for(let i=0;i<o;i+=3){const p=e[i],l=i+1<o?e[i+1]:0,r=i+2<o?e[i+2]:0,s=p>>2,c=(p&3)<<4|l>>4,u=(l&15)<<2|r>>6,d=r&63;n+=t[s]+t[c]+t[u]+t[d]}return n}function V(e,t){const n=new TextEncoder().encode(e),o=t(n,{level:9,to:"Uint8Array"});return F(o)}async function j(e){if(e.length===0)return;const{deflate:t}=await x(async()=>{const{deflate:o}=await import("./pako.esm.DYFMLp3J.js");return{deflate:o}},[]);f=!1;const n=Array.from(e);for(const o of n){if(f)return;const i=o.getAttribute("data-content");if(!i)continue;const p="plantuml-"+Math.round(Math.random()*1e5),r=`https://www.plantuml.com/plantuml/svg/~1${V(i,t)}`;try{const s=await fetch(r);if(f)return;if(!s.ok)throw new Error(`Failed to fetch PlantUML diagram: ${s.status}`);const c=await s.text();if(f)return;const u=B();u.innerHTML=c,o.innerHTML="",o.appendChild(u);const d=u.querySelector("svg");d&&await H(d,u,p,{diagramContent:i})}catch(s){console.warn("PlantUML SVG fetch failed, falling back to img:",s);const c=document.createElement("img");c.src=r,c.alt="PlantUML diagram",c.loading="lazy",c.style.margin="0 auto",c.style.display="block",o.innerHTML="",o.appendChild(c)}}}export{j as a,O as d,W as r};
