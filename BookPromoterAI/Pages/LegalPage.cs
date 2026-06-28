namespace BookPromoterAI;

static class LegalPage
{
    public static string TermsAndConditions() => $"""
        <section class="panel legal-page">
            <header class="legal-header">
                <p class="eyebrow">Legal</p>
                <h1>Terms &amp; Conditions</h1>
                <p class="muted">Last updated: {DateTime.UtcNow:MMMM d, yyyy} (UTC)</p>
                <p class="legal-intro">
                    Please read these Terms &amp; Conditions (&ldquo;Terms&rdquo;) carefully before using BookPromoter AI.
                    By accessing or using the service, creating an account, or purchasing a subscription, you agree to be bound by these Terms.
                </p>
            </header>

            {Section("1. Who we are",
                """
                <p>BookPromoter AI (&ldquo;BookPromoter AI,&rdquo; &ldquo;we,&rdquo; &ldquo;us,&rdquo; or &ldquo;our&rdquo;) is operated by Melanie Botha, the owner and operator of the BookPromoter AI platform available at <strong>bookpromoterai.us</strong> and related subdomains (the &ldquo;Service&rdquo;).</p>
                <p>These Terms form a binding agreement between you (&ldquo;you,&rdquo; &ldquo;User,&rdquo; or &ldquo;Customer&rdquo;) and Melanie Botha.</p>
                """)}

            {Section("2. Acceptance of terms",
                """
                <p>By using the Service — including browsing the website, registering, logging in, requesting an access code, subscribing, uploading content, generating posts, or sending emails through the platform — you confirm that you:</p>
                <ul>
                    <li>Have read and understood these Terms;</li>
                    <li>Agree to comply with them; and</li>
                    <li>Are at least 18 years old (or the age of majority in your jurisdiction) and have the legal capacity to enter into this agreement.</li>
                </ul>
                <p>If you do not agree, you must not use the Service.</p>
                """)}

            {Section("3. Copyright &amp; intellectual property",
                """
                <p><strong>Our property.</strong> The Service, including but not limited to its software, source code, design, layout, logos, branding, text, graphics, features, workflows, and documentation, is owned by Melanie Botha and is protected by copyright, trademark, and other intellectual property laws.</p>
                <p>&copy; {DateTime.UtcNow.Year} Melanie Botha. All rights reserved.</p>
                <p>Except for the limited license granted below, no part of the Service may be copied, modified, distributed, sold, leased, reverse-engineered, decompiled, or used to create a competing product without our prior written consent.</p>
                <p><strong>Your content.</strong> You retain ownership of content you submit (such as book titles, descriptions, cover images, store links, mailing list data, and generated post text). You grant us a non-exclusive, worldwide, royalty-free license to host, store, process, display, and transmit your content solely to operate and improve the Service.</p>
                """)}

            {Section("4. License to use the Service",
                """
                <p>Subject to these Terms and your active access (trial, access code, or paid subscription), we grant you a limited, non-exclusive, non-transferable, revocable license to use the Service for your personal or internal business book-promotion purposes.</p>
                <p>You may not sublicense, resell, white-label, or make the Service available to third parties except as expressly permitted by your subscription plan (for example, agency features where offered).</p>
                """)}

            {Section("5. Accounts &amp; security",
                """
                <p>You are responsible for maintaining the confidentiality of your login credentials and for all activity under your account. Notify us promptly at <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a> if you suspect unauthorized access.</p>
                <p>We may suspend or terminate accounts that violate these Terms or pose a security risk.</p>
                """)}

            {Section("6. Subscriptions, billing &amp; refunds",
                """
                <p>Paid plans, pricing, and features are described on the Service. Payments are processed by third-party providers (such as Stripe). By subscribing, you authorize recurring charges according to your selected plan until you cancel.</p>
                <p>Fees are generally non-refundable except where required by applicable law or explicitly stated otherwise. Downgrades and cancellations take effect at the end of the current billing period unless stated otherwise.</p>
                <p>We may change plan prices or features with reasonable notice. Continued use after a price change constitutes acceptance of the new pricing.</p>
                """)}

            {Section("7. No guarantee of results",
                """
                <p><strong>BookPromoter AI is a marketing and productivity tool only.</strong> We do not guarantee any particular outcome from using the Service, including but not limited to:</p>
                <ul>
                    <li>Book sales, royalties, or revenue;</li>
                    <li>Reader sign-ups, mailing list growth, or email open rates;</li>
                    <li>Social media followers, likes, shares, impressions, or engagement;</li>
                    <li>Rankings on Amazon, other retailers, or bestseller lists;</li>
                    <li>Acceptance, approval, or performance of posts on any social platform; or</li>
                    <li>Any specific return on investment (ROI).</li>
                </ul>
                <p>Your results depend on many factors outside our control, including your books, pricing, market conditions, platform algorithms, and your own marketing efforts. <strong>Any examples, statistics, or descriptions on the Service are illustrative only and not promises of future performance.</strong></p>
                """)}

            {Section("8. Social media, email &amp; third-party platforms",
                """
                <p>The Service may help you draft posts, schedule content, track links, or send emails. You are solely responsible for:</p>
                <ul>
                    <li>The accuracy and legality of content you publish or send;</li>
                    <li>Compliance with each platform&rsquo;s terms (Facebook, Instagram, X, TikTok, Amazon, etc.);</li>
                    <li>Disclosures required for advertising, affiliate links, or sponsored content; and</li>
                    <li>Obtaining consent from recipients before sending marketing emails.</li>
                </ul>
                <p>Where integrations are simulated or not yet connected to live APIs, you understand that posting may require manual action on your part. We are not responsible for rejected posts, account restrictions, or penalties imposed by third-party platforms.</p>
                <p>Third-party services (payment processors, email providers, hosting, analytics) are governed by their own terms. We are not liable for outages or actions of those providers.</p>
                """)}

            {Section("9. Acceptable use",
                """
                <p>You agree not to use the Service to:</p>
                <ul>
                    <li>Violate any law or third-party rights;</li>
                    <li>Upload malware, spam, or unlawful content;</li>
                    <li>Harass, defame, or impersonate others;</li>
                    <li>Scrape, overload, or attempt unauthorized access to our systems;</li>
                    <li>Misrepresent book details, reviews, or sales claims; or</li>
                    <li>Resell or redistribute the Service without authorization.</li>
                </ul>
                """)}

            {Section("10. Disclaimer of warranties",
                """
                <p>THE SERVICE IS PROVIDED <strong>&ldquo;AS IS&rdquo;</strong> AND <strong>&ldquo;AS AVAILABLE,&rdquo;</strong> WITHOUT WARRANTIES OF ANY KIND, WHETHER EXPRESS, IMPLIED, OR STATUTORY, INCLUDING IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, TITLE, AND NON-INFRINGEMENT.</p>
                <p>We do not warrant that the Service will be uninterrupted, error-free, secure, or free of harmful components, or that generated content will be accurate, complete, or suitable for your purposes.</p>
                """)}

            {Section("11. Limitation of liability",
                """
                <p>To the fullest extent permitted by law, Melanie Botha and BookPromoter AI shall not be liable for any indirect, incidental, special, consequential, or punitive damages, or for any loss of profits, revenue, data, goodwill, book sales, or business opportunities arising from or related to your use of the Service — even if we have been advised of the possibility of such damages.</p>
                <p>Our total aggregate liability for any claim arising out of or relating to the Service or these Terms shall not exceed the greater of (a) the amount you paid us in the twelve (12) months before the event giving rise to the claim, or (b) one hundred U.S. dollars (USD $100).</p>
                <p>Some jurisdictions do not allow certain limitations; in those cases, our liability is limited to the maximum extent permitted by law.</p>
                """)}

            {Section("12. Indemnification",
                """
                <p>You agree to defend, indemnify, and hold harmless Melanie Botha, BookPromoter AI, and our affiliates, officers, and agents from any claims, damages, losses, liabilities, and expenses (including reasonable attorneys&rsquo; fees) arising from:</p>
                <ul>
                    <li>Your use of the Service;</li>
                    <li>Your content, posts, emails, or promotions;</li>
                    <li>Your violation of these Terms or applicable law; or</li>
                    <li>Your infringement of any third-party rights.</li>
                </ul>
                """)}

            {Section("13. Termination",
                """
                <p>You may stop using the Service at any time. We may suspend or terminate your access if you breach these Terms, fail to pay applicable fees, or if we discontinue the Service.</p>
                <p>Sections that by their nature should survive termination (including intellectual property, disclaimers, limitation of liability, and indemnification) will survive.</p>
                """)}

            {Section("14. Changes to these Terms",
                """
                <p>We may update these Terms from time to time. When we do, we will revise the &ldquo;Last updated&rdquo; date at the top of this page. Material changes may also be communicated through the Service or by email.</p>
                <p>Your continued use after changes become effective constitutes acceptance of the revised Terms.</p>
                """)}

            {Section("15. Governing law &amp; disputes",
                """
                <p>These Terms are governed by the laws of the United States, without regard to conflict-of-law principles.</p>
                <p>Any dispute arising from these Terms or the Service shall be resolved in the state or federal courts located in the United States, and you consent to their exclusive jurisdiction, except where prohibited by law.</p>
                """)}

            {Section("16. Contact",
                """
                <p>Questions about these Terms may be sent to:</p>
                <p><strong>Melanie Botha</strong><br>
                Email: <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a><br>
                Website: <a href="https://bookpromoterai.us">bookpromoterai.us</a></p>
                """)}

            <p class="legal-footer-note muted small-text">
                By using BookPromoter AI, you acknowledge that you have read, understood, and agree to these Terms &amp; Conditions.
                See also our <a href="/privacy">Privacy Policy</a>.
            </p>
        </section>
        """;

    public static string PrivacyPolicy() => $"""
        <section class="panel legal-page">
            <header class="legal-header">
                <p class="eyebrow">Legal</p>
                <h1>Privacy Policy</h1>
                <p class="muted">Last updated: {DateTime.UtcNow:MMMM d, yyyy} (UTC)</p>
                <p class="legal-intro">
                    This Privacy Policy explains how BookPromoter AI (&ldquo;we,&rdquo; &ldquo;us,&rdquo; or &ldquo;our&rdquo;) collects, uses, stores, and shares information when you use
                    <strong>bookpromoterai.us</strong> and related services (the &ldquo;Service&rdquo;). It should be read together with our
                    <a href="/terms">Terms &amp; Conditions</a>.
                </p>
            </header>

            {Section("1. Who we are",
                """
                <p>The Service is operated by <strong>Melanie Botha</strong>, owner of BookPromoter AI.</p>
                <p>For privacy questions or requests, contact us at <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a>.</p>
                """)}

            {Section("2. Information we collect",
                """
                <p>We collect information you provide directly, information generated through your use of the Service, and limited technical data needed to operate the platform.</p>
                <p><strong>Account information</strong></p>
                <ul>
                    <li>Email address and password (stored as a secure hash, not plain text)</li>
                    <li>Account codes, subscription status, and access type (trial, paid plan, etc.)</li>
                    <li>Terms acceptance date and version</li>
                </ul>
                <p><strong>Content you add to the Service</strong></p>
                <ul>
                    <li>Book details (titles, authors, genres, descriptions, cover images, store links)</li>
                    <li>Generated or edited social posts and ad library content</li>
                    <li>Social account connection details (platform, display name, handle)</li>
                    <li>Mailing list subscriber names and email addresses you collect through the Service</li>
                    <li>Feedback, bug reports, and support messages you submit</li>
                </ul>
                <p><strong>Usage and technical data</strong></p>
                <ul>
                    <li>Click tracking on promo links you create</li>
                    <li>Posting logs and scheduling preferences</li>
                    <li>Session cookies required to keep you logged in</li>
                    <li>Basic server logs (such as IP address, browser type, and pages requested) maintained by our hosting provider</li>
                </ul>
                <p><strong>Payment information</strong></p>
                <ul>
                    <li>Subscriptions are processed by <strong>Stripe</strong>. We do not store full credit card numbers on our servers. Stripe provides us with identifiers and billing status needed to manage your subscription.</li>
                </ul>
                """)}

            {Section("3. How we use your information",
                """
                <p>We use collected information to:</p>
                <ul>
                    <li>Create and manage your account</li>
                    <li>Provide book promotion tools, post generation, scheduling, and analytics</li>
                    <li>Send transactional emails (access codes, password resets, team invites, product updates you request as owner)</li>
                    <li>Send emails you initiate (mailing list campaigns, owner broadcasts)</li>
                    <li>Process subscriptions and prevent fraud</li>
                    <li>Respond to feedback and improve the Service</li>
                    <li>Comply with legal obligations and enforce our Terms</li>
                </ul>
                <p>We do not sell your personal information to third parties.</p>
                """)}

            {Section("4. Email communications",
                """
                <p>We use <strong>SendGrid</strong> to deliver email. Depending on how you use the Service, emails may include:</p>
                <ul>
                    <li>Account and security messages (password reset, access codes)</li>
                    <li>Thank-you messages after feedback submissions</li>
                    <li>Mailing list messages you send to your own subscribers</li>
                    <li>Product update or promotional messages from the site owner</li>
                </ul>
                <p>If you are an author using the mailing list feature, <strong>you are responsible</strong> for having a lawful basis to email your subscribers and for honoring unsubscribe requests, in addition to following this Privacy Policy and our Terms.</p>
                """)}

            {Section("5. Third-party services",
                """
                <p>We rely on trusted providers to operate the Service. These may process data on our behalf:</p>
                <ul>
                    <li><strong>Stripe</strong> — subscription billing and payment processing</li>
                    <li><strong>SendGrid</strong> — transactional and user-initiated email delivery</li>
                    <li><strong>Railway</strong> (or similar hosting) — application hosting and data storage</li>
                    <li><strong>Social platforms</strong> — when you connect accounts or post content (Facebook, Instagram, X, Bluesky, etc.), those platforms&rsquo; own privacy policies apply</li>
                </ul>
                <p>We only share information with these providers as needed to deliver the Service. Each third party processes data under its own terms and privacy policy.</p>
                """)}

            {Section("6. Cookies and session data",
                """
                <p>We use essential cookies and similar technologies to:</p>
                <ul>
                    <li>Keep you logged in during your session</li>
                    <li>Protect forms against cross-site request forgery (CSRF)</li>
                </ul>
                <p>We do not use third-party advertising cookies on the Service at this time. You can control cookies through your browser settings, but disabling essential cookies may prevent you from logging in.</p>
                """)}

            {Section("7. How long we keep data",
                """
                <p>We retain information for as long as your account is active or as needed to provide the Service, comply with law, resolve disputes, and enforce our agreements.</p>
                <ul>
                    <li><strong>Account data</strong> — until you delete your account or ask us to delete it, subject to legal retention requirements</li>
                    <li><strong>Billing records</strong> — as required for tax, accounting, and fraud prevention</li>
                    <li><strong>Server logs</strong> — for a limited period according to our hosting provider&rsquo;s practices</li>
                </ul>
                <p>You may delete your account from the Service where that option is available, or contact us to request deletion.</p>
                """)}

            {Section("8. Security",
                """
                <p>We use reasonable technical and organizational measures to protect your information, including encrypted passwords, HTTPS, and secure session handling. No method of transmission or storage is 100% secure, and we cannot guarantee absolute security.</p>
                <p>Please use a strong password and notify us immediately at <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a> if you believe your account has been compromised.</p>
                """)}

            {Section("9. Your rights and choices",
                """
                <p>Depending on where you live, you may have rights to:</p>
                <ul>
                    <li>Access the personal information we hold about you</li>
                    <li>Correct inaccurate information</li>
                    <li>Request deletion of your account and associated data</li>
                    <li>Object to or restrict certain processing</li>
                    <li>Withdraw consent where processing is based on consent</li>
                    <li>Receive a copy of your data in a portable format</li>
                </ul>
                <p>To exercise these rights, email <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a>. We may need to verify your identity before fulfilling a request.</p>
                <p>If you are in the European Economic Area or United Kingdom, you may also have the right to lodge a complaint with your local data protection authority.</p>
                """)}

            {Section("10. International users",
                """
                <p>BookPromoter AI is operated from the United States. If you access the Service from outside the U.S., your information may be transferred to, stored, and processed in the United States or other countries where our service providers operate.</p>
                <p>By using the Service, you understand that your information may be processed in jurisdictions that may have different data protection laws than your country of residence.</p>
                """)}

            {Section("11. Children&rsquo;s privacy",
                """
                <p>The Service is not intended for anyone under 18 years of age (or the age of majority in your jurisdiction). We do not knowingly collect personal information from children. If you believe a child has provided us with personal information, contact us and we will take steps to delete it.</p>
                """)}

            {Section("12. Mailing list subscribers (readers)",
                """
                <p>When readers sign up to an author&rsquo;s mailing list through a BookPromoter AI signup link, we store their name and email address on behalf of that author. The author controls how that list is used. Readers should contact the author directly for list-related requests, or contact us if they need help reaching the account holder.</p>
                """)}

            {Section("13. Changes to this Privacy Policy",
                """
                <p>We may update this Privacy Policy from time to time. When we do, we will revise the &ldquo;Last updated&rdquo; date at the top of this page. Material changes may be communicated through the Service or by email where appropriate.</p>
                <p>Your continued use of the Service after changes become effective means you accept the updated Privacy Policy.</p>
                """)}

            {Section("14. Contact us",
                """
                <p>Questions about this Privacy Policy or our data practices:</p>
                <p><strong>Melanie Botha</strong><br>
                Email: <a href="mailto:bothamelanief@gmail.com">bothamelanief@gmail.com</a><br>
                Website: <a href="https://bookpromoterai.us">bookpromoterai.us</a></p>
                """)}

            <p class="legal-footer-note muted small-text">
                See also our <a href="/terms">Terms &amp; Conditions</a>.
            </p>
        </section>
        """;

    public static string AcceptTerms(string notice) => $"""
        <section class="panel legal-page legal-accept-panel">
            <header class="legal-header">
                <p class="eyebrow">Required</p>
                <h1>Accept Terms &amp; Conditions</h1>
                <p class="legal-intro">
                    Before you can use BookPromoter AI, you must read and accept our Terms &amp; Conditions.
                    This includes copyright protection, disclaimers about book sales and marketing results, and limits on our liability.
                </p>
            </header>

            {notice}

            <div class="legal-accept-summary panel">
                <h2>Summary</h2>
                <ul>
                    <li>BookPromoter AI is owned by Melanie Botha and protected by copyright.</li>
                    <li>We provide marketing tools only — <strong>we do not guarantee book sales, revenue, followers, or any specific results</strong>.</li>
                    <li>You are responsible for your own posts, emails, and compliance with social platforms and laws.</li>
                    <li>The service is provided &ldquo;as is&rdquo; with limited liability as described in the full Terms.</li>
                </ul>
                <p><a href="/terms" target="_blank" rel="noopener"><strong>Read the full Terms &amp; Conditions</strong></a> and <a href="/privacy" target="_blank" rel="noopener"><strong>Privacy Policy</strong></a>.</p>
            </div>

            <form method="post" action="/accept-terms" class="form legal-accept-form">
                <label class="checkbox-label legal-accept-checkbox">
                    <input type="checkbox" name="acceptTerms" value="true" required>
                    <span>I have read and agree to the <a href="/terms" target="_blank" rel="noopener">Terms &amp; Conditions</a> (version {H.Encode(LegalConstants.CurrentTermsVersion)}).</span>
                </label>
                <button class="button" type="submit">Accept and continue</button>
            </form>
        </section>
        """;

    static string Section(string title, string html) => $"""
        <article class="legal-section">
            <h2>{H.Encode(title)}</h2>
            {html}
        </article>
        """;
}
