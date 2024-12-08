import { test, expect } from '@playwright/test';
import { LoginPage } from 'e2e-tests/pages/login-page';


const url = 'https://demo.kavitareader.com/';

test('has title', async ({ page }) => {
  await page.goto(url);

  // Expect a title "to contain" a substring.
  await expect(page).toHaveTitle(/Kavita/);
});

test('login functionality', async ({ page }) => {
  // Navigate to the login page
  await page.goto(url);

  // Verify the page title
  await expect(page).toHaveTitle(/Kavita/);

  const loginPage = new LoginPage(page);
  await loginPage.navigate();
  await loginPage.login('demouser', 'Demouser64');

  // Verify successful login by checking for Home on side nav
  await expect(page.locator('#null')).toBeVisible();
});
