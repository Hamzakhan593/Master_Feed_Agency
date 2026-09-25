/* Searchable customer/product choices. Original selects remain the submitted source. */
(() => {
 'use strict';
 let serial = 0;
 const normal = s => String(s || '').normalize('NFKD').replace(/[\u0300-\u036f]/g,'').toLowerCase().replace(/[^\p{L}\p{N}]/gu,'');
 function enhance(select, settings = {}) {
  if (select.dataset.enhanced) return select._search;
  select.dataset.enhanced='true';
  const wrap=document.createElement('div');wrap.className='choice-search';
  const input=document.createElement('input');input.type='text';input.className='form-control choice-input';input.autocomplete='off';input.spellcheck=false;
  input.id='choice-'+(++serial);input.placeholder=settings.placeholder || select.dataset.searchPlaceholder || 'Naam ya phone se search karein';
  input.setAttribute('role','combobox');input.setAttribute('aria-autocomplete','list');input.setAttribute('aria-expanded','false');
  input.required=!!settings.required || select.hasAttribute('data-search-required');
  input.setAttribute('aria-label',settings.label || select.dataset.searchLabel || 'Customer search');
  const list=document.createElement('div');list.id=input.id+'-list';list.className='choice-results';list.setAttribute('role','listbox');list.hidden=true;input.setAttribute('aria-controls',list.id);
  const status=document.createElement('div');status.className='visually-hidden';status.setAttribute('role','status');
  const clear=document.createElement('button');clear.type='button';clear.className='choice-clear';clear.textContent='×';clear.setAttribute('aria-label','Selection clear karein');
  select.before(wrap);wrap.append(input,clear,status);document.body.append(list);select.hidden=true;select.tabIndex=-1;
  const label=select.id && document.querySelector('label[for="'+CSS.escape(select.id)+'"]');if(label)label.htmlFor=input.id;
  let matches=[],active=-1,dirty=false;
  const selectedText=()=>select.selectedOptions[0]?.textContent.trim() || '';
  function sync(){input.value=select.value && select.value!=='0' ? selectedText() : '';input.placeholder=select.value==='' && select.id==='customerSelect'?'Customer ka naam ya phone search karein':input.placeholder;dirty=false;input.setCustomValidity('');}
  function close(){list.hidden=true;input.setAttribute('aria-expanded','false');input.removeAttribute('aria-activedescendant');}
  function position(){const r=input.getBoundingClientRect();const below=(window.visualViewport?.height||innerHeight)-r.bottom-(innerWidth<992?85:12);list.style.left=Math.max(8,Math.min(r.left,innerWidth-280))+'px';list.style.width=Math.min(Math.max(r.width,280),innerWidth-16)+'px';const height=Math.min(300,Math.max(120,below));list.style.maxHeight=height+'px';list.style.top=(below<140?Math.max(8,r.top-Math.min(300,r.top-8)):r.bottom+4)+'px';}
  function highlight(i){active=i;[...list.querySelectorAll('[role=option]')].forEach((n,j)=>{n.setAttribute('aria-selected',String(i===j));if(i===j){input.setAttribute('aria-activedescendant',n.id);n.scrollIntoView({block:'nearest'});}});}
  function choose(option){select.value=option.value;sync();close();select.dispatchEvent(new Event('input',{bubbles:true}));select.dispatchEvent(new Event('change',{bubbles:true}));settings.onChoose?.(option);if(option.value&&select.dataset.searchFocus)document.querySelector(select.dataset.searchFocus)?.focus();}
  function render(){
   const term=normal(dirty?input.value:'');matches=[...select.options].filter(o=>!o.disabled && normal(o.textContent+' '+(o.dataset.search||'')+' '+(o.dataset.detail||'')).includes(term));
   list.replaceChildren();const total=matches.length;matches=matches.slice(0,30);
   matches.forEach((option,i)=>{const item=document.createElement('div');item.className='choice-option';item.id=list.id+'-'+i;item.setAttribute('role','option');item.setAttribute('aria-selected','false');
    const title=document.createElement('strong');title.textContent=option.textContent;item.append(title);
    if(option.dataset.detail){const detail=document.createElement('small');detail.textContent=option.dataset.detail;item.append(detail);}
    item.addEventListener('mousedown',e=>e.preventDefault());item.addEventListener('click',()=>choose(option));list.append(item);
   });
   if(!total||total>30){const note=document.createElement('p');note.className='choice-note';note.textContent=!total?'Koi result nahi mila. Naam ya phone dobara check karein.':'Aur results ke liye naam ya code ke mazeed letters likhein.';list.append(note);}
   active=-1;list.hidden=false;input.setAttribute('aria-expanded','true');position();status.textContent=total+' results';if(matches.length)highlight(0);
  }
  input.addEventListener('focus',render);input.addEventListener('input',()=>{dirty=true;input.setCustomValidity('Neeche list se sahi result select karein.');render();});
  input.addEventListener('keydown',e=>{if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();if(list.hidden)render();highlight(Math.max(0,Math.min(matches.length-1,active+(e.key==='ArrowDown'?1:-1))));}else if(e.key==='Enter'&&!list.hidden){e.preventDefault();if(matches[active])choose(matches[active]);}else if(e.key==='Escape'){e.preventDefault();sync();close();}});
  input.addEventListener('blur',()=>setTimeout(()=>close(),150));
  clear.addEventListener('click',()=>{const empty=[...select.options].find(o=>o.value===''||o.value==='0');if(empty)choose(empty);input.focus();});
  select.addEventListener('change',sync);const onResize=()=>{if(!list.hidden)position();};window.addEventListener('resize',onResize);const onScroll=e=>{if(!list.hidden&&!list.contains(e.target)){const r=input.getBoundingClientRect();if(r.bottom<0||r.top>innerHeight)close();else position();}};window.addEventListener('scroll',onScroll,true);
  select.form?.addEventListener('reset',()=>setTimeout(sync,0));sync();
  const api={input,close,destroy(){list.remove();wrap.remove();select.hidden=false;delete select.dataset.enhanced;window.removeEventListener('resize',onResize);window.removeEventListener('scroll',onScroll,true);}};
  select._search=api;return api;
 }
 window.AgencySearch={enhance};
 document.addEventListener('DOMContentLoaded',()=>{
  document.querySelectorAll('select[data-search]').forEach(s=>enhance(s));
  // Optional details stay out of the main flow, but never hide errors or entered values.
  document.querySelectorAll('details.ux-extra').forEach(d=>{if(d.querySelector('.input-validation-error')||[...d.querySelectorAll('input,textarea')].some(x=>x.type==='checkbox'?x.checked:!!x.value&&x.value!=='0'&&x.value!=='0.00'))d.open=true;});
  // Remember list URLs within this tab; forms and financial entries are not persisted.
  const path=location.pathname.replace(/\/Index$/i,'').replace(/\/$/,'');
  try{
   if(['/Customers','/Products'].includes(path))sessionStorage.setItem('agency-list:'+path,location.pathname+location.search);
   document.querySelectorAll('a[data-return-list]').forEach(a=>{const target=a.dataset.returnList;const saved=sessionStorage.getItem('agency-list:'+target);if(saved&&saved.startsWith(target)&&!saved.startsWith('//'))a.href=saved;});
   document.querySelectorAll('form[action*="Logout"]').forEach(f=>f.addEventListener('submit',()=>{sessionStorage.removeItem('agency-list:/Customers');sessionStorage.removeItem('agency-list:/Products');}));
  }catch{}
 });
})();
(() => {
 document.addEventListener('DOMContentLoaded',()=>{
  const form=document.querySelector('.customer-filter-grid,.product-filter-grid');if(!form)return;
  const isCustomer=form.classList.contains('customer-filter-grid');
  const panel=isCustomer?'.customer-list-panel':'.product-list-panel';const stats=isCustomer?'.customer-stat-grid':'.product-stat-grid';
  const notice=document.createElement('p');notice.className='small mt-2';notice.setAttribute('role','status');form.after(notice);
  let timer,request;
  async function search(){
   request?.abort();request=new AbortController();const controller=request;
   const url=new URL(form.action||location.href);url.search=new URLSearchParams(new FormData(form)).toString();
   notice.textContent='Search ho rahi hai...';
   try{
    const response=await fetch(url,{signal:controller.signal});if(!response.ok)throw Error();
    const doc=new DOMParser().parseFromString(await response.text(),'text/html');
    if(!doc.querySelector(panel))throw Error();if(controller.signal.aborted)return;
    for(const selector of [panel,stats]){const current=document.querySelector(selector),next=doc.querySelector(selector);if(current&&next)current.replaceWith(document.importNode(next,true));}
    history.replaceState(null,'',url);try{sessionStorage.setItem('agency-list:'+(isCustomer?'/Customers':'/Products'),url.pathname+url.search);}catch{}
    notice.textContent='List update ho gayi.';
   }catch(e){if(e.name!=='AbortError')notice.textContent='Search update nahi hui. Search button se dobara koshish karein.';}
  }
  form.addEventListener('input',()=>{clearTimeout(timer);request?.abort();timer=setTimeout(search,300);});
  form.addEventListener('change',()=>{clearTimeout(timer);search();});
  form.addEventListener('submit',e=>{e.preventDefault();clearTimeout(timer);search();});
 });
})();
