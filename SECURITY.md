# Security Policy

## Supported Versions

We release patches for security vulnerabilities in the following versions:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

We take the security of WinLive seriously. If you believe you have found a security vulnerability, please report it to us as described below.

### Please Do NOT

- Open a public GitHub issue
- Disclose the vulnerability publicly before it has been addressed

### Please Do

1. **Email us directly** at [INSERT SECURITY EMAIL HERE]
2. **Include the following information**:
   - Type of vulnerability
   - Full description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact
   - Suggested fix (if any)
   - Your name/handle (for acknowledgment)

### What to Expect

- **Initial Response**: Within 48 hours
- **Status Updates**: Regular updates on progress
- **Fix Timeline**: We aim to address critical vulnerabilities within 7 days
- **Credit**: We will acknowledge your contribution (unless you prefer to remain anonymous)

## Security Best Practices

When using WinLive:

### For Users

1. **Download from Official Sources**: Only download releases from GitHub
2. **Verify Signatures**: Check release signatures when available
3. **Keep Updated**: Use the latest version

### For Developers

1. **Dependencies**: Regularly update dependencies
2. **Code Review**: All code changes should be reviewed
3. **Input Validation**: Validate all user inputs
4. **Secrets**: Never commit secrets or API keys
5. **Testing**: Include security tests in the test suite

## Security Features

WinLive implements the following security features:

### Data Storage

- **Local-Only**: All data is stored locally in SQLite
- **No Cloud Sync**: No data is sent to external servers (except feed fetching and translation)
- **No Analytics**: No tracking or analytics are collected

### Code Security

- **Input Sanitization**: All user inputs are sanitized
- **SQL Injection Protection**: Parameterized queries prevent SQL injection
- **XSS Prevention**: Vue.js escapes content by default
- **Dependency Scanning**: Automated dependency vulnerability scanning

## Security Updates

Security updates are released as soon as possible after a vulnerability is confirmed:

1. **Critical**: Immediate patch release
2. **High**: Within 7 days
3. **Medium**: Within 30 days
4. **Low**: Next regular release

## Disclosure Policy

- **Coordinated Disclosure**: We follow a coordinated disclosure policy
- **Public Disclosure**: Vulnerabilities are disclosed after a patch is available
- **Credit**: Security researchers are credited in release notes

## Additional Resources

- [OWASP Top Ten](https://owasp.org/www-project-top-ten/)
- [CWE Top 25](https://cwe.mitre.org/top25/)
- [Go Security](https://golang.org/security/)
- [Vue.js Security](https://vuejs.org/guide/best-practices/security.html)

## Contact

For security-related questions that are not vulnerabilities:

- Contact us at [mail@ch3nyang.top](mailto:mail@ch3nyang.top)

---

Thank you for helping keep WinLive and its users safe!
